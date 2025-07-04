using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using MdnsListener.Interfaces;
using MdnsListener.Models;

namespace MdnsListener.Services;

/// <summary>
/// A secure DNS packet parser that implements input validation and
/// protection against malformed packets.
/// </summary>
public sealed class SecureDnsPacketParser : IDnsPacketParser
{
    private const int MinDnsHeaderSize = 12;
    private const int MaxDnsNameLength = 255;
    private const int MaxLabelLength = 63;
    private const int MaxCompressionJumps = 10;
    private const int MaxResourceRecords = 100;

    private readonly ILogger<SecureDnsPacketParser> _logger;

    public SecureDnsPacketParser(ILogger<SecureDnsPacketParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public DnsPacket? Parse(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < MinDnsHeaderSize)
        {
            _logger.LogWarning("Packet too small for DNS header ({Length} bytes)", buffer.Length);
            return null;
        }

        if (buffer.Length > 65535)
        {
            _logger.LogWarning("Packet exceeds maximum UDP size ({Length} bytes)", buffer.Length);
            return null;
        }

        try
        {
            var reader = new SecureDnsReader(buffer, _logger);
            return ParsePacket(ref reader);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid DNS packet structure");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing DNS packet");
            return null;
        }
    }

    private DnsPacket ParsePacket(ref SecureDnsReader reader)
    {
        // Parse header
        var transactionId = reader.ReadUInt16();
        var flags = reader.ReadUInt16();
        var questionCount = reader.ReadUInt16();
        var answerCount = reader.ReadUInt16();
        var authorityCount = reader.ReadUInt16();
        var additionalCount = reader.ReadUInt16();

        // Validate counts
        var totalRecords = questionCount + answerCount + authorityCount + additionalCount;
        if (totalRecords > MaxResourceRecords)
        {
            throw new InvalidOperationException(
                $"Too many resource records ({totalRecords}), maximum allowed is {MaxResourceRecords}");
        }

        // Skip questions (we don't process them in mDNS responses)
        for (var i = 0; i < questionCount; i++)
        {
            reader.SkipName();
            reader.Skip(4); // qtype (2) + qclass (2)
        }

        // Parse answer records
        var answers = ReadRecords(ref reader, answerCount);

        // Skip authority records (typically empty in mDNS)
        for (var i = 0; i < authorityCount; i++)
        {
            SkipResourceRecord(ref reader);
        }

        // Parse additional records
        var additionals = ReadRecords(ref reader, additionalCount);

        return new DnsPacket
        {
            TransactionId = transactionId,
            IsResponse = (flags & 0x8000) != 0,
            AnswerRecords = answers,
            AdditionalRecords = additionals
        };
    }

    private IReadOnlyList<ResourceRecord> ReadRecords(ref SecureDnsReader reader, int count)
    {
        if (count == 0)
        {
            return Array.Empty<ResourceRecord>();
        }

        var records = new List<ResourceRecord>(Math.Min(count, MaxResourceRecords));
        
        for (var i = 0; i < count; i++)
        {
            var record = ParseResourceRecord(ref reader);
            if (record != null)
            {
                records.Add(record);
            }
        }
        
        return records;
    }

    private ResourceRecord? ParseResourceRecord(ref SecureDnsReader reader)
    {
        var name = reader.ReadName();
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (reader.Remaining < 10)
        {
            throw new InvalidOperationException("Insufficient data for resource record");
        }

        var type = reader.ReadUInt16();
        var rrClass = reader.ReadUInt16();
        var ttl = reader.ReadUInt32();
        var dataLength = reader.ReadUInt16();

        if (dataLength > reader.Remaining)
        {
            throw new InvalidOperationException(
                $"Resource record data length ({dataLength}) exceeds remaining buffer");
        }

        var data = reader.ReadBytes(dataLength);

        return new ResourceRecord
        {
            Name = name,
            Type = (DnsRecordType)type,
            Class = rrClass,
            Ttl = ttl,
            Data = data
        };
    }

    private void SkipResourceRecord(ref SecureDnsReader reader)
    {
        reader.SkipName();
        
        if (reader.Remaining < 10)
        {
            throw new InvalidOperationException("Insufficient data for resource record");
        }
        
        reader.Skip(8); // type (2) + class (2) + ttl (4)
        var dataLength = reader.ReadUInt16();
        
        if (dataLength > reader.Remaining)
        {
            throw new InvalidOperationException("Resource record data length exceeds buffer");
        }
        
        reader.Skip(dataLength);
    }

    /// <summary>
    /// A secure DNS reader that implements bounds checking and
    /// protection against malicious input.
    /// </summary>
    private ref struct SecureDnsReader
    {
        private readonly ReadOnlySpan<byte> _buffer;
        private readonly ILogger _logger;
        private int _offset;
        private readonly HashSet<int> _visitedOffsets;

        public SecureDnsReader(ReadOnlyMemory<byte> buffer, ILogger logger)
        {
            _buffer = buffer.Span;
            _logger = logger;
            _offset = 0;
            _visitedOffsets = new HashSet<int>();
        }

        public int Remaining => _buffer.Length - _offset;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int bytes)
        {
            if (Remaining < bytes)
            {
                throw new InvalidOperationException($"Buffer underrun: need {bytes} bytes, have {Remaining}");
            }
        }

        public ushort ReadUInt16()
        {
            EnsureCapacity(2);
            var value = BinaryPrimitives.ReadUInt16BigEndian(_buffer.Slice(_offset, 2));
            _offset += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            EnsureCapacity(4);
            var value = BinaryPrimitives.ReadUInt32BigEndian(_buffer.Slice(_offset, 4));
            _offset += 4;
            return value;
        }

        public ReadOnlyMemory<byte> ReadBytes(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
            }

            EnsureCapacity(length);
            var slice = _buffer.Slice(_offset, length);
            _offset += length;
            return new ReadOnlyMemory<byte>(slice.ToArray());
        }

        public void Skip(int bytes)
        {
            if (bytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes), "Cannot skip negative bytes");
            }

            EnsureCapacity(bytes);
            _offset += bytes;
        }

        public void SkipName() => ReadName(skipOnly: true);

        public string ReadName(bool skipOnly = false)
        {
            var labels = skipOnly ? null : new List<string>();
            var nameLength = 0;
            var originalOffset = _offset;
            var jumped = false;
            var jumps = 0;

            // Clear visited offsets for each name to allow reuse in different contexts
            _visitedOffsets.Clear();

            while (jumps < MaxCompressionJumps)
            {
                if (_offset >= _buffer.Length)
                {
                    throw new InvalidOperationException("Name extends beyond buffer");
                }

                // Detect compression loops
                if (!_visitedOffsets.Add(_offset))
                {
                    throw new InvalidOperationException("Compression pointer loop detected");
                }

                var len = _buffer[_offset];
                
                if (len == 0)
                {
                    _offset++;
                    break;
                }

                if ((len & 0xC0) == 0xC0) // Compression pointer
                {
                    if (_offset + 1 >= _buffer.Length)
                    {
                        throw new InvalidOperationException("Compression pointer extends beyond buffer");
                    }

                    if (!jumped)
                    {
                        originalOffset = _offset + 2;
                        jumped = true;
                    }

                    var pointer = ((len & 0x3F) << 8) | _buffer[_offset + 1];
                    
                    if (pointer >= _buffer.Length)
                    {
                        throw new InvalidOperationException($"Invalid compression pointer: {pointer}");
                    }

                    if (pointer >= originalOffset)
                    {
                        throw new InvalidOperationException("Forward compression pointer detected");
                    }

                    _offset = pointer;
                    jumps++;
                    continue;
                }

                if (len > MaxLabelLength)
                {
                    throw new InvalidOperationException($"Label length {len} exceeds maximum {MaxLabelLength}");
                }

                _offset++;
                
                if (_offset + len > _buffer.Length)
                {
                    throw new InvalidOperationException("Label extends beyond buffer");
                }

                if (!skipOnly)
                {
                    nameLength += len + 1; // Label + dot
                    
                    if (nameLength > MaxDnsNameLength)
                    {
                        throw new InvalidOperationException($"Name length exceeds maximum {MaxDnsNameLength}");
                    }

                    var label = Encoding.UTF8.GetString(_buffer.Slice(_offset, len));
                    labels!.Add(label);
                }

                _offset += len;
            }

            if (jumps >= MaxCompressionJumps)
            {
                throw new InvalidOperationException("Too many compression jumps");
            }

            if (jumped)
            {
                _offset = originalOffset;
            }

            return labels is null ? string.Empty : string.Join(".", labels);
        }
    }
}