# Bash Completion for MdnsListener

This directory contains bash completion scripts for the MdnsListener application.

## Installation

### Option 1: Source directly (temporary)

```bash
source completion/mdnslistener-completion.bash
```

### Option 2: Install for current user

```bash
# Create bash completion directory if it doesn't exist
mkdir -p ~/.local/share/bash-completion/completions

# Copy the completion script
cp completion/mdnslistener-completion.bash ~/.local/share/bash-completion/completions/mdnslistener

# Reload your shell or source your bashrc
source ~/.bashrc
```

### Option 3: System-wide installation (requires sudo)

```bash
# Copy to system bash completion directory
sudo cp completion/mdnslistener-completion.bash /etc/bash_completion.d/mdnslistener

# Reload your shell or source the completion
source /etc/bash_completion.d/mdnslistener
```

## Usage

After installation, you can use Tab completion with MdnsListener:

```bash
# Complete options
MdnsListener --<TAB>

# Complete log levels
MdnsListener --log-level <TAB>

# Complete DNS record types
MdnsListener --service-type <TAB>

# Complete config files (only .json files)
MdnsListener --config <TAB>
```

## Features

- Completes all command-line options
- Provides log level suggestions (Trace, Debug, Information, Warning, Error, Critical)
- Provides DNS record type suggestions (A, AAAA, PTR, SRV, TXT, etc.)
- Suggests common service name patterns
- Suggests common domain patterns
- File completion for --config option (filters for .json files)
- Supports multi-value options (space-separated values)

## Customization

You can edit the completion script to add your own common service names or patterns. Look for the sections with `_http._tcp.local`, `_ipp._tcp.local`, etc., and add your own commonly used services.