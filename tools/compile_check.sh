#!/bin/bash
# Fast compile check without Unity. Safe to run in parallel. Prints only errors (and a final OK/FAILED line).
set -u
P=/home/dvojkaa/git/ShelterSim; cd "$P"
D=/home/dvojkaa/Unity/Hub/Editor/6000.6.8f1/Editor/Data
export DOTNET_ROOT=$D/DotNetSdk
DN=$D/DotNetSdk/dotnet; [ -x "$DN" ] || DN=$D/NetCoreRuntime/dotnet
CSC=$D/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll
T=$(mktemp -d); trap 'rm -rf "$T"' EXIT
R=$(ls Library/Bee/artifacts/2400b0aE.dag/Assembly-CSharp.rsp); RE=$(ls Library/Bee/artifacts/2400b0aE.dag/Assembly-CSharp-Editor.rsp)
filt() { grep -v -E '^-out:|^-refout:|^"?Assets/|^"?Packages/|^-ruleset|^-analyzer|^-a:|^-additionalfile|^-errorlog|^-generatedfilesout|^-warnaserror|^"Library/Bee/.*\.cs' "$1" | grep -v 'Assembly-CSharp.ref.dll\|ScriptAssemblies/Assembly-CSharp.dll' ; }
{ filt "$R"; echo "-out:\"$T/Assembly-CSharp.dll\""; echo "-nowarn:CS0618,CS0649,CS0414,CS0169,CS0219,CS0162,CS0067,CS0108"; find Assets/Scripts -name '*.cs' | sed 's/.*/"&"/'; } > "$T/a.rsp"
"$DN" "$CSC" -nologo -noconfig "@$T/a.rsp" 2>&1 | grep -E 'error|Unhandled' | sort -u > "$T/e1"
{ filt "$RE"; echo "-r:\"$T/Assembly-CSharp.dll\""; echo "-out:\"$T/Assembly-CSharp-Editor.dll\""; echo "-nowarn:CS0618,CS0649,CS0414,CS0169,CS0219,CS0162,CS0067,CS0108"; find Assets/Editor -name '*.cs' | sed 's/.*/"&"/'; } > "$T/e.rsp"
if [ ! -s "$T/e1" ]; then "$DN" "$CSC" -nologo -noconfig "@$T/e.rsp" 2>&1 | grep -E 'error|Unhandled' | sort -u > "$T/e2"; fi
cat "$T/e1" "$T/e2" 2>/dev/null
if [ -s "$T/e1" ] || [ -s "$T/e2" ]; then echo "COMPILE FAILED"; exit 1; else echo "COMPILE OK ($(find Assets/Scripts -name '*.cs'|wc -l) runtime + $(find Assets/Editor -name '*.cs'|wc -l) editor files)"; fi
