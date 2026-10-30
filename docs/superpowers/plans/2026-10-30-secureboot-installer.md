# Secure Boot Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Entregar um instalador experimental com preparação e assinatura locais, teste USB, estados verificáveis e instalação/restauração gerenciada da cadeia assinada.

**Architecture:** Componentes C# independentes para identidade/assinatura, coleta/construção SteamOS e sessão/transação. O firmware escreve relatório de teste vinculado à sessão. A GUI coordena os componentes e explica as confirmações manuais.

**Tech Stack:** C#/.NET Framework 4.8, PowerShell Windows para CNG/Authenticode, C UEFI, coletor Linux e QEMU/OVMF.

**Spec:** docs/superpowers/specs/2026-10-30-secureboot-installer-design.md

## Global Constraints

- Sem alterações no boot, certificados de produção ou firmware físico do PC de desenvolvimento.
- Não distribuir chaves privadas, kernels ou configurações particulares.
- PK/KEK/db/dbx, TPM, BitLocker e BCD preservados; MOK e BIOS confirmados pelo usuário.
- Manter o fluxo público 0.2.0 e a leitura/restauração de seus journals.
- Operações desconhecidas/incompatíveis são bloqueadas; testes físicos não são inferidos de emulação.
- Trabalhar em feat/secureboot-installer, no checkout existente.

## Review Focus

- Um PE modificado após a assinatura deve ser recusado mesmo que o certificado ainda esteja presente.
- Relatório antigo ou de outra sessão não promove instalação para pronta.
- Um pacote SteamOS importado não pode executar comandos ou escapar de seu diretório.
- Falha após cópia/metadados deve deixar restauração coerente sem tocar arquivos alheios.
- Reabertura da GUI deve retomar a sessão correta e preservar instalações personalizadas.

### Task 1: Local identity and cryptographic signatures

**Files:** Create installer/SecureSigning.cs, installer/SecureSigningTests.cs.
**Interfaces:** SigningIdentity fields Thumbprint, CertificateBase64, CertificateSha256; LocalSigner.CreateOrOpen(string sessionRoot), Sign(string input,string output,SigningIdentity identity), Verify(string path,string certificateBase64). No certificate creation merely by scanning/opening GUI.
- [x] Testar adulteração PE, certificado diferente, limites e geração explícita de identidade não exportável.
- [x] Implementar CNG/Authenticode via Windows PowerShell e verificação criptográfica independente de confiança da raiz.
- [x] Executar testes e relatar limitações sem escrever certificados de produção.

### Task 2: SteamOS collector and authenticated GRUB bundle

**Files:** Create installer/SteamCollection.cs, installer/SteamBundle.cs, installer/SteamCollectionTests.cs, scripts/collect-secureboot-steamos.sh.
**Interfaces:** SteamCollection.Load(string directory) returns SteamInventory; SteamBundle.Build(SteamInventory inventory, Func<byte[],byte[]> signKernel) returns unsigned self-contained GRUB PE. Inventory contains explicit partition GUIDs, slot/version, kernel arguments, source root and validated file records. Collector is a resource copied for the user; no WSL assumption.
- [x] Testar manifestos inválidos, escapes, hashes, layouts PE/GRUB e argumentos com injeção.
- [x] Coletar apenas sistema ativo compatível, recusar oobe e ambiguidades; preparar core GRUB com módulos necessários.
- [x] Construir memdisk autenticado com kernel assinado, initramfs/microcódigo, configuração e guardião dos hashes originais.
- [x] Testar com fixtures sintéticos e arquivos do inventário existente apenas em leitura.

### Task 3: Portable EFI test report

**Files:** Create src/secureboot_report.c, src/secureboot_report.h, scripts/build-secureboot-payload.ps1; modify src/main.c, scripts/build.ps1 as necessary.
**Interfaces:** Unsigned template with reserved certificate/public session binding region patched before signing. Report Format=DualBootSecureBootStatusV2, SessionId (32 lowercase hex), CertificateSHA256 (64 hex), SecureBoot, SetupMode, LocalCertificateInMokListRT, ShimProtocolPresent, ShimValidationDisabled. No firmware writes. db_secureboot_report(DbPlatform*) called under build flag.
- [x] Testar parser do patch e relatório, ausência de MOK/shim, limites e sessão incorreta.
- [x] Compilar templates públicos sem certificado particular e incluir SBAT.
- [x] Testar aceitação/recusa em OVMF com certificados de laboratório existentes, sem criar identidade de produção.

### Task 4: Session state, signed transactions and restoration

**Files:** Create installer/SecureBootWorkflow.cs, installer/SecureBootDeployment.cs, installer/SecureBootTests.cs.
**Interfaces:** SecureBootSession v1 with own Id, public identity, source identities, file hashes, selected Windows/SteamOS, stages and journals. Signed payload distinct from legacy payload; preserve legacy Version=2 journals. Installation only after session-bound USB test, current policy gates and source/target readback.
- [x] Testar estados incompletos, relatórios antigos, destinos concorrentes, falhas e recuperação.
- [x] Integrar componentes 1–3 e transações existentes, listas explícitas de arquivos e limites SteamOS adequados.
- [x] Preservar carregadores originais, instalar bundle SteamOS próprio e configuração que carrega cópia assinada; não modificar slots existentes.
- [x] Verificação e reversão resolvem GUIDs atuais e recusam alterações posteriores.

### Task 5: GUI, packaging and documentation

**Files:** Create installer/SecureBootForm.cs; modify MainForm.cs, Program.cs, build-installer.ps1, README.md, docs/INSTALADOR.md; add docs/SECUREBOOT.md and provenance manifest.
- [x] Integrar preparar/retomar, exportar coletor, importar coleta, certificado, USB, verificar teste, SSD e restauração.
- [x] Incorporar shim/MokManager oficiais e templates com hashes, licenças e origem verificados.
- [x] Gerar versão 0.3.0 experimental, EXE/ZIP/hashes e captura de demonstração.
- [x] Executar testes antigos e novos, QEMU positivo/negativos e revisão independente; corrigir achados antes de concluir.

### Task 6: Delivery review

- [x] Registrar o que foi testado e o que ainda depende de hardware físico.
- [x] Garantir ausência de identidade, chave ou inventário privado nos artefatos.
- [x] Preparar os artefatos e descrição da release para revisão, sem publicação automática.

## Execution ledger

Decisão: usar componentes independentes com agentes separados; integrar e revisar no controlador. Compartilhar somente interfaces explícitas e arquivos de responsabilidade. Nenhuma segunda aprovação de planejamento é necessária após a aprovação da proposta.
