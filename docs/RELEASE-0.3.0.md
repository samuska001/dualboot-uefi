# Proposta de release 0.3.0 experimental

Artefatos preparados localmente em 30/10/2026, ainda sem publicação desta versão no GitHub. Branch `feat/secureboot-installer`.

O novo assistente prepara identidades e assinaturas locais para cada computador, orienta teste por pendrive, cadastro MOK e ativação manual na BIOS e permite instalação/restauração gerenciada após as verificações. O modo tradicional continua separado e os journals 0.2.0 permanecem suportados.

## Artefatos revisados

- `DualBoot-Setup-0.3.0.exe`, 2.460.160 bytes, SHA256 `fb14d180eb845f270d2080b656fbc3786dcf449a8f064664e6c8dcdd3233fd1d`.
- `DualBoot-Setup-0.3.0.zip`, SHA256 `d4fa8b767c442841bc54b62eb12a57b4953890099fb2dfd1ca8e614cc7ed13ea`.
- `SHA256SUMS-0.3.0.txt`.

O ZIP contém somente o EXE, dois manuais, LICENSE, THIRD_PARTY, procedência e licença do shim. O EXE dentro do ZIP tem o mesmo hash do artefato separado. Não contém chaves, kernels ou inventários particulares.

## Validação

Build empacotado: 28 verificações core, 30 de deployment tradicional, 28 de workflow/transações assinadas e 34 de coleta/bundle; harness WinForms de recuperação aprovado. EXE final passou `--self-test` e captura `--render-secure-demo` sem operações nativas. Núcleo EFI e 12 cenários QEMU tradicionais passaram.

Assinatura CNG real com duas identidades descartáveis de laboratório: criação, reabertura, assinatura, rejeição de adulteração/certificado diferente e remoção das duas chaves/certificados conferidas. OVMF com shim/MOK real aceitou as imagens assinadas e recusou unsigned/adulterada/outro certificado; relatório V2 confirmou SecureBoot=1 e validação habilitada.

Revisões independentes: corrigidos limite de leitura do handle da coleta, vínculo durável sessão/journal, reconciliação de estado e atualização imediata dos controles de recuperação. Revisão final e re-revisão do fix não deixaram achados P0/P1/P2 pendentes.

**Limite da entrega:** o fluxo completo do EXE ainda precisa de teste físico em outro computador, incluindo coleta ao vivo e boot do novo bundle SteamOS. Não foi aplicado sobre o boot personalizado deste PC. As confirmações MOK/BIOS continuam manuais e não existe promessa de compatibilidade universal ou de assinatura automática de futuras atualizações SteamOS.
