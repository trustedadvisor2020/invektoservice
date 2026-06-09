# Memory Health Check -- powershell -ExecutionPolicy Bypass -File arch\check-memory-health.ps1
# Exit 0 saglikli / 1 hard-check FAIL (cap-asti veya marker okuma penceresinde yok). READ-ONLY, ASCII-only.
$ErrorActionPreference='Stop'; $root=Split-Path -Parent $PSScriptRoot; $rw=320
$hard=@(
 @{File='arch/session-memory.md';Cap=300;Marker='END_CURRENT_STATE'},
 @{File='arch/hot-lessons.md';   Cap=80; Marker=$null})
$fail=$false
"{0,-26} {1,7} {2,6}  {3}" -f 'DOSYA (auto-load)','satir','cap','DURUM'; "-"*64
foreach($c in $hard){ $p=Join-Path $root $c.File
 if(-not(Test-Path $p)){"{0,-26} {1,7} {2,6}  [YOK]" -f $c.File,'-',$c.Cap;$fail=$true;continue}
 $n=(Get-Content $p).Count
 if($n -le $c.Cap){$s='[PASS]'}else{$s='[FAIL cap-asti]';$fail=$true}
 if($c.Marker){ if((Get-Content $p -TotalCount $rw) -match $c.Marker){$s+=" marker<=$rw OK"}else{$s+=' [FAIL marker pencerede YOK]';$fail=$true} }
 "{0,-26} {1,7} {2,6}  {3}" -f $c.File,$n,$c.Cap,$s }
$cm=Join-Path $root 'CLAUDE.md'; if(Test-Path $cm){ $n=(Get-Content $cm).Count; if($n -le 180){$w='OK'}else{$w='[WARN slim-adayi]'}; "{0,-26} {1,7} {2,6}  {3}" -f 'CLAUDE.md (info)',$n,180,$w }
"`nGrep-only (buyuk olabilir, sorun degil):"
foreach($f in 'arch/lessons-learned.md','arch/lessons-learned-archive.md','arch/session-memory-archive.md','arch/docs/decisions.md'){ $p=Join-Path $root $f; if(Test-Path $p){"  {0,-38} {1,7} satir" -f $f,(Get-Content $p).Count} }
if($fail){"`nSONUC: FAIL -- /optimize-memory ile sikistir.";exit 1}else{"`nSONUC: PASS (hard checks)";exit 0}
