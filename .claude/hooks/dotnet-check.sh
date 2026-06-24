#!/bin/bash
# PostToolUse hook: targeted dotnet build after .cs edits
# Reads tool JSON from stdin, extracts file path, runs dotnet build on the owning csproj.
#
# NOTE: Bu hook varsayilan olarak settings.json'a bagli DEGILDIR. Gercek dotnet build
# yavas oldugu icin her .cs edit'te calistirmak yerine build-reminder.ps1 kullanilir.
# Bu dosya TONIVA tsc-check.sh'ye paralel, opsiyonel drop-in olarak tutuluyor.
#
# Opt-in icin settings.json'a ekle:
# {
#   "matcher": "Edit|Write",
#   "hooks": [{
#     "type": "command",
#     "command": "bash c:/CRMs/InvektoServices/.claude/hooks/dotnet-check.sh",
#     "timeout": 60,
#     "statusMessage": "Building..."
#   }]
# }

INPUT=$(cat)
FILE=$(echo "$INPUT" | node -e "
  let d='';
  process.stdin.on('data',c=>d+=c);
  process.stdin.on('end',()=>{
    try {
      const j = JSON.parse(d);
      const f = String(j.tool_input?.file_path || j.tool_response?.filePath || '');
      console.log(f);
    } catch { console.log(''); }
  });
")

# Skip non-cs files
if [[ ! "$FILE" =~ \.cs$ ]]; then
  exit 0
fi

# Skip auto-generated files
if [[ "$FILE" =~ \.Designer\.cs$ ]] || [[ "$FILE" =~ \.g\.cs$ ]] || [[ "$FILE" =~ /obj/ ]] || [[ "$FILE" =~ /bin/ ]]; then
  exit 0
fi

# Determine which service csproj to build
SERVICE=""
if [[ "$FILE" =~ src/Chatinbox\.([A-Za-z]+)/ ]]; then
  SERVICE="${BASH_REMATCH[1]}"
elif [[ "$FILE" =~ src\\Chatinbox\.([A-Za-z]+)\\ ]]; then
  SERVICE="${BASH_REMATCH[1]}"
fi

if [[ -z "$SERVICE" ]]; then
  exit 0
fi

CSPROJ="c:/CRMs/InvektoServices/src/Chatinbox.${SERVICE}/Chatinbox.${SERVICE}.csproj"
if [[ ! -f "$CSPROJ" ]]; then
  exit 0
fi

# Targeted build, quiet output, first 20 lines of diagnostics
dotnet build "$CSPROJ" --no-restore -v q 2>&1 | head -20

exit 0
