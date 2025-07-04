namespace MdnsListener.Models;

/// <summary>
/// Standard DNS record types.
/// </summary>
public enum DnsRecordType : ushort
{
    A = 1,
    CNAME = 5,
    PTR = 12,
    TXT = 16,
    AAAA = 28,
    SRV = 33
}
