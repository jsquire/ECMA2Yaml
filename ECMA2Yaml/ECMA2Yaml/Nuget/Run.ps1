﻿param (
    [parameter(mandatory=$true)]
    [hashtable]$ParameterDictionary
)

$currentDir = $($MyInvocation.MyCommand.Definition) | Split-Path
$ecma2yamlExeName = "ECMA2Yaml.exe"

# Main
$errorActionPreference = 'Stop'

$repositoryRoot = $ParameterDictionary.environment.repositoryRoot
$logFilePath = $ParameterDictionary.environment.logFile
$logOutputFolder = $currentDictionary.environment.logOutputFolder
$cacheFolder = $currentDictionary.environment.cacheFolder
$outputFolder = $currentDictionary.environment.outputFolder

$dependentFileListFilePath = $ParameterDictionary.context.dependentFileListFilePath
$changeListTsvFilePath = $ParameterDictionary.context.changeListTsvFilePath
$userSpecifiedChangeListTsvFilePath = $ParameterDictionary.context.userSpecifiedChangeListTsvFilePath

pushd $repositoryRoot
$publicBranch = 'master'

$publicGitUrl = & git config --get remote.origin.url
if ($publicGitUrl.EndsWith(".git"))
{
    $publicGitUrl = $publicGitUrl.Substring(0, $publicGitUrl.Length - 4)
}
& git branch | foreach {
    if ($_ -match "^\* (.*)") {
        $publicBranch = $matches[1]
    }
}
popd

if (-not [string]::IsNullOrEmpty($ParameterDictionary.environment.publishConfigContent.git_repository_url_open_to_public_contributors))
{
    $publicGitUrl = $ParameterDictionary.environment.publishConfigContent.git_repository_url_open_to_public_contributors
}
if (-not [string]::IsNullOrEmpty($ParameterDictionary.environment.publishConfigContent.git_repository_branch_open_to_public_contributors))
{
    $publicBranch = $ParameterDictionary.environment.publishConfigContent.git_repository_branch_open_to_public_contributors
}
if (-not $publicGitUrl.EndsWith("/"))
{
    $publicGitUrl += "/"
}
$jobs = $ParameterDictionary.docset.docsetInfo.ECMA2Yaml
if (!$jobs)
{
	$jobs = $ParameterDictionary.environment.publishConfigContent.ECMA2Yaml
}
if ($jobs -isnot [system.array])
{
    $jobs = @($jobs)
}
foreach($ecmaConfig in $jobs)
{
    $ecmaXmlGitUrlBase = $publicGitUrl + "blob/" + $publicBranch
    echo "Using $ecmaXmlGitUrlBase as url base"
	$fallbackRepo = Join-Path $repositoryRoot _repo.en-us
	$ecmaFallbackSourceXmlFolder = Join-Path $fallbackRepo $ecmaConfig.SourceXmlFolder
    $ecmaSourceXmlFolder = Join-Path $repositoryRoot $ecmaConfig.SourceXmlFolder
    $ecmaOutputYamlFolder = Join-Path $repositoryRoot $ecmaConfig.OutputYamlFolder
    $allArgs = @("-s", "$ecmaSourceXmlFolder", "-o", "$ecmaOutputYamlFolder", "-l", "$logFilePath", "-p", """$repositoryRoot=>$ecmaXmlGitUrlBase""");
	if (Test-Path $ecmaFallbackSourceXmlFolder)
	{
		$allArgs += "-fs";
		$allArgs += "$ecmaFallbackSourceXmlFolder";
	}
    if ($ecmaConfig.Flatten)
    {
        $allArgs += "-f";
    }
    if ($ecmaConfig.StrictMode)
    {
        $allArgs += "-strict";
    }
    if (-not [string]::IsNullOrEmpty($ecmaConfig.SourceMetadataFolder))
    {
        $ecmaSourceMetadataFolder = Join-Path $repositoryRoot $ecmaConfig.SourceMetadataFolder
		if (Test-Path $ecmaSourceMetadataFolder)
		{
			$allArgs += "-m";
            $allArgs += "$ecmaSourceMetadataFolder";
		}
    }
	$changeListFile = $ParameterDictionary.context.changeListTsvFilePath;
	if (-not [string]::IsNullOrEmpty($changeListFile) -and (Test-Path $changeListFile))
	{
		$newChangeList = $changeListFile -replace "\.tsv$",".mapped.tsv";
		$allArgs += "-changeList";
        $allArgs += "$changeListFile";
		$ParameterDictionary.context.changeListTsvFilePath = $newChangeList
	}
	$userChangeListFile = $ParameterDictionary.context.userSpecifiedChangeListTsvFilePath;
	if (-not [string]::IsNullOrEmpty($userChangeListFile) -and (Test-Path $userChangeListFile))
	{
		$newUserChangeList = $userChangeListFile -replace "\.tsv$",".mapped.tsv";
		$allArgs += "-changeList";
        $allArgs += "$userChangeListFile";
		$ParameterDictionary.context.userSpecifiedChangeListTsvFilePath = $newUserChangeList
	}
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