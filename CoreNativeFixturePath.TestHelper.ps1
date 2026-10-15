# Test-only descendant prefix shared by the native lifecycle fixture and its focused test.
function Get-CoreNativeFixtureDescendantPrefix([string]$root) {
 return [IO.Path]::GetFullPath($root).TrimEnd('\','/')+'\'
}
