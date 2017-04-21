﻿param (
    [parameter(mandatory=$true)]
    [hashtable]$ParameterDictionary
)

$currentDir = $($MyInvocation.MyCommand.Definition) | Split-Path
$ecma2yamlExeName = "ECMA2Yaml.exe"

# Main
$errorActionPreference = 'Stop'

$logFilePath = $ParameterDictionary.environment.logFile

$jobs = $ParameterDictionary.environment.publishConfigContent.JoinTOCPlugin
if ($jobs -isnot [system.array])
{
    $jobs = @($jobs)
}
foreach($JoinTOCConfig in $jobs)
{
	$topTOC = Join-Path $repositoryRoot $JoinTOCConfig.TopLevelTOC
	$refTOC = Join-Path $repositoryRoot $JoinTOCConfig.ReferenceTOC
    $allArgs = @("-joinTOC", "-topLevelTOC", "$topTOC", "-refTOC", "$refTOC", "-l", "$logFilePath");

    $printAllArgs = [System.String]::Join(' ', $allArgs)
    $ecma2yamlExeFilePath = Join-Path $currentDir $ecma2yamlExeName
    echo "Executing $ecma2yamlExeFilePath $printAllArgs" | timestamp
    & "$ecma2yamlExeFilePath" $allArgs
    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }

    if (-not [string]::IsNullOrEmpty($ecmaConfig.id))
    {
        $tocPath = Join-Path $ecmaOutputYamlFolder "toc.yml"
        $newTocPath = Join-Path $ecmaOutputYamlFolder $ecmaConfig.id
        if (-not (Test-Path $newTocPath))
        {
            New-Item -ItemType Directory -Force -Path $newTocPath
        }
        Move-Item $tocPath $newTocPath
    }
}
