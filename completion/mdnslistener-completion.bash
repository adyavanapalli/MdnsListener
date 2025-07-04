#!/bin/bash
# Bash completion script for MdnsListener

_mdnslistener_completion()
{
    local cur prev opts
    COMPREPLY=()
    cur="${COMP_WORDS[COMP_CWORD]}"
    prev="${COMP_WORDS[COMP_CWORD-1]}"

    # All available options
    opts="--service-name --service-type --domain-pattern --all --log-level --config --help --version"

    # Log levels for --log-level completion
    log_levels="Trace Debug Information Warning Error Critical"

    # DNS record types for --service-type completion
    dns_types="A AAAA CNAME MX NS PTR SOA SRV TXT"

    case "${prev}" in
        --log-level)
            COMPREPLY=( $(compgen -W "${log_levels}" -- ${cur}) )
            return 0
            ;;
        --service-type)
            COMPREPLY=( $(compgen -W "${dns_types}" -- ${cur}) )
            return 0
            ;;
        --config|-c)
            # File completion for config files
            COMPREPLY=( $(compgen -f -X '!*.json' -- ${cur}) )
            return 0
            ;;
        --service-name)
            # For service names, we can't predict them, so just show common patterns
            if [[ -z "$cur" ]]; then
                COMPREPLY=( $(compgen -W "_http._tcp.local _ipp._tcp.local _airplay._tcp.local _raop._tcp.local _homekit._tcp.local" -- ${cur}) )
            fi
            return 0
            ;;
        --domain-pattern)
            # Common domain patterns
            if [[ -z "$cur" ]]; then
                COMPREPLY=( $(compgen -W "*.local *._tcp.local *._udp.local" -- ${cur}) )
            fi
            return 0
            ;;
    esac

    # If current word starts with -, show options
    if [[ ${cur} == -* ]] ; then
        COMPREPLY=( $(compgen -W "${opts}" -- ${cur}) )
        return 0
    fi

    # Check if we're in a multi-value option context
    local i
    for (( i=1; i < ${#COMP_WORDS[@]}-1; i++ )); do
        case "${COMP_WORDS[i]}" in
            --service-name|--service-type|--domain-pattern)
                # If the previous word wasn't an option, we're still in multi-value mode
                if [[ "${COMP_WORDS[i+1]}" != --* ]]; then
                    case "${COMP_WORDS[i]}" in
                        --service-type)
                            COMPREPLY=( $(compgen -W "${dns_types}" -- ${cur}) )
                            ;;
                        --service-name)
                            # Additional service name patterns
                            COMPREPLY=( $(compgen -W "_ssh._tcp.local _smb._tcp.local _afpovertcp._tcp.local _nfs._tcp.local" -- ${cur}) )
                            ;;
                        --domain-pattern)
                            COMPREPLY=( $(compgen -W "*._http._tcp.local *._https._tcp.local" -- ${cur}) )
                            ;;
                    esac
                    return 0
                fi
                ;;
        esac
    done
}

# Register completion for both the full path and just the command name
complete -F _mdnslistener_completion MdnsListener
complete -F _mdnslistener_completion mdnslistener
complete -F _mdnslistener_completion ./MdnsListener