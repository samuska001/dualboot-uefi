# Run only in the explicitly authorized repository's release workflow.
$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true' -or $env:GITHUB_REPOSITORY -ne 'samuska001/dualboot-uefi' -or $env:GITHUB_REF -ne 'refs/heads/main' -or $env:GITHUB_SHA -notmatch '\A[0-9a-f]{40}\z' -or -not $env:GITHUB_TOKEN){throw 'Use the authorized main-branch GitHub Actions release workflow.'}
$root=Split-Path -Parent $PSScriptRoot
$api='https://api.github.com/repos/samuska001/dualboot-uefi'
$headers=@{Authorization=('Bearer '+$env:GITHUB_TOKEN);Accept='application/vnd.github+json';'X-GitHub-Api-Version'='2022-11-28'}
$names=@('DualBoot-Setup-0.4.0.exe','DualBoot-Setup-0.4.0.zip','DualBoot-SteamOS-Tools-0.4.0.zip','SHA256SUMS-0.4.0.txt')
$hashes=@{}
foreach($name in $names){$file=Join-Path $root ('dist\'+$name);if(-not (Test-Path -LiteralPath $file -PathType Leaf)){throw ('Artifact missing: '+$name)};$hashes[$name]=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()}
$checksums=[IO.File]::ReadAllText((Join-Path $root 'dist\SHA256SUMS-0.4.0.txt'))
foreach($name in $names[0..2]){if(-not $checksums.Contains($hashes[$name]+'  '+$name)){throw 'Artifact checksum manifest mismatch'}}
$body=@'
## Menu atualizado e ferramentas SteamOS — experimental

Baixe **DualBoot-Setup-0.4.0.exe** ou o ZIP com os manuais. O menu ganhou foco centralizado de 180 ms, transparência e redução de trabalho no framebuffer. O bundle assinado silencia somente o formato conhecido de checksum correto, preservando a verificação e os erros. O pacote **DualBoot-SteamOS-Tools-0.4.0.zip** traz o seletor opcional de monitor/TV e diagnósticos de inicialização, vídeo e animação. O aplicativo prepara uma identidade exclusiva e assina cópias EFI no computador de destino, orienta o teste por USB e oferece instalação/restauração gerenciadas.

- Cadastro do certificado no MokManager e ativação na BIOS continuam **manuais**.
- Não modifica PK/KEK/db/dbx, BCD, TPM ou BitLocker; preserva instalações personalizadas existentes.
- Suporte SteamOS inicial experimental para o perfil 3.9.x compatível; atualizações exigem coleta, assinatura e teste novos.
- O EXE Windows ainda não tem assinatura comercial Authenticode. A assinatura local dos arquivos EFI é uma função separada.

[Manual do Secure Boot](https://github.com/samuska001/dualboot-uefi/blob/v0.4.0/docs/SECUREBOOT.md) · [Validação](https://github.com/samuska001/dualboot-uefi/blob/v0.4.0/docs/VALIDACAO.md)

Testes locais: assinatura CNG real com identidades descartáveis removidas após o teste; UEFI virtual com shim/MOK aceitando imagens assinadas e recusando unsigned/adulteradas/outro certificado; 12 cenários QEMU tradicionais; testes de estados, coleta, transações e recuperação. Compilação e testes do instalador também executados pelo GitHub Actions antes da publicação.

**Ainda falta validar fisicamente o fluxo completo deste EXE em outro PC**, incluindo coleta ao vivo, novo bundle SteamOS e instalação/restauração SSD. Emulação não significa compatibilidade universal. A instalação personalizada do desenvolvedor foi preservada.

Compare os arquivos com **SHA256SUMS-0.4.0.txt**. Não são distribuídas chaves privadas, kernels ou inventários particulares. Os arquivos “Source code” do GitHub são os fontes, não o EXE pronto.
'@
$release=$null
try{$release=Invoke-RestMethod -Headers $headers -Uri ($api+'/releases/tags/v0.4.0')}
catch{if([int]$_.Exception.Response.StatusCode -ne 404){throw}}
if($release -and -not $release.draft){throw 'v0.4.0 is already published; existing release preserved.'}
if($release -and $release.target_commitish -ne $env:GITHUB_SHA){throw 'Existing draft points to another commit; preserve for review.'}
if(-not $release){$payload=@{tag_name='v0.4.0';target_commitish=$env:GITHUB_SHA;name='DualBoot Setup 0.4.0 — Secure Boot experimental';body=$body;draft=$true;prerelease=$true;make_latest='false'} | ConvertTo-Json;$release=Invoke-RestMethod -Method Post -Headers $headers -Uri ($api+'/releases') -Body $payload -ContentType 'application/json; charset=utf-8'}
foreach($name in $names){
 $existing=@($release.assets | Where-Object name -eq $name)
 if($existing.Count){if($existing.Count -ne 1 -or $existing[0].digest -ne ('sha256:'+$hashes[$name])){throw ('Existing draft artifact differs: '+$name)};continue}
 $upload='https://uploads.github.com/repos/samuska001/dualboot-uefi/releases/'+$release.id+'/assets?name='+[Uri]::EscapeDataString($name)
 $asset=Invoke-RestMethod -Method Post -Headers $headers -Uri $upload -InFile (Join-Path $root ('dist\'+$name)) -ContentType 'application/octet-stream'
 if($asset.state -ne 'uploaded' -or $asset.digest -ne ('sha256:'+$hashes[$name])){throw ('Uploaded artifact digest mismatch: '+$name)}
}
$verified=Invoke-RestMethod -Headers $headers -Uri ($api+'/releases/'+$release.id)
if($verified.assets.Count -ne 4){throw 'Unexpected draft asset set; release remains a draft.'}
foreach($name in $names){$asset=@($verified.assets | Where-Object name -eq $name);if($asset.Count -ne 1 -or $asset[0].digest -ne ('sha256:'+$hashes[$name])){throw 'Draft readback mismatch'}}
$published=Invoke-RestMethod -Method Patch -Headers $headers -Uri ($api+'/releases/'+$release.id) -Body (@{draft=$false;prerelease=$true;body=$body;make_latest='false'} | ConvertTo-Json) -ContentType 'application/json; charset=utf-8'
if($published.draft -or -not $published.prerelease){throw 'Release publication state mismatch'}
Write-Output ('Published verified experimental release: '+$published.html_url)
