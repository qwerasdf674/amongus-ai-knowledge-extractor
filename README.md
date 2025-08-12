# Knowledge Base Generator for AI (dnSpy-style)

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/) [![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](./LICENSE) [![ILSpy Engine](https://img.shields.io/badge/Decompile-ILSpy-orange)](https://github.com/icsharpcode/ILSpy)

Tool to decompile .NET assemblies (e.g., Unity’s `Assembly-CSharp.dll`) and generate a readable, dnSpy-style knowledge base. Ideal for exploring game code and preparing data for LLMs.

— Portuguese version below —

## Highlights

- __On-demand detail__: full, dnSpy-like decompilation for selected classes
- __Optimized parallelization__: fast analysis for the rest (fields, properties, events, ctors, methods)
- __Structured output__: `namespace → type → members`, consistent indentation
- __Console UX__: progress bar and colored steps ([1/3], [2/3], [3/3])
- __Post-processing__: removes noisy IL comments (`//IL_...`)
- __Separate index__: lists all types and members

## Quick start

1) Build: `dotnet build`
2) Drag and drop your `Assembly-CSharp.dll` onto the built executable
3) Open the generated `.txt` files next to the DLL

CLI (PowerShell):

```powershell
./bin/Debug/net9.0/ConsoleApp2.exe "C:\\Path\\To\\Assembly-CSharp.dll"
```

## Configuration (`Program.cs`)

- `CLASSES_PARA_DETALHE_COMPLETO`
- `NAMESPACES_PARA_INCLUIR` / `NAMESPACES_PARA_IGNORAR`
- `MAXIMO_TOKENS_APROXIMADO`

## License

MIT

Ferramenta para decompilar assemblies .NET (ex.: `Assembly-CSharp.dll` de Unity) e gerar uma base de conhecimento legível em estilo dnSpy. Ideal para explorar código de jogos e preparar dados para LLMs.

— Versão em português abaixo —

## Destaques

- __Detalhe sob demanda__: decompilação completa, estilo dnSpy, para classes selecionadas
- __Paralelismo otimizado__: análise rápida do restante (campos, propriedades, eventos, construtores, métodos)
- __Saída organizada__: `namespace → tipo → membros`, com identação consistente
- __UX no console__: barra de progresso e etapas coloridas ([1/3], [2/3], [3/3])
- __Limpeza pós-processamento__: remoção de comentários IL ruidosos (`//IL_...`)
- __Índice separado__: arquivo com todos os tipos e membros

## O que é gerado

- `[AssemblyName]-knowledge-base-index.txt`: índice legível com namespaces, tipos e membros
- `[AssemblyName]-knowledge-base-completo.txt`: base principal com o código decompilado

## Requisitos

- .NET SDK 9.0+

## Como usar

1) Compile o projeto: `dotnet build`
2) Arraste e solte seu `Assembly-CSharp.dll` sobre o executável gerado
3) Abra os `.txt` gerados na mesma pasta do seu DLL

Via terminal (PowerShell):

```powershell
./bin/Debug/net9.0/ConsoleApp2.exe "C:\\Path\\To\\Assembly-CSharp.dll"
```

## Configuração (em `Program.cs`)

- __`CLASSES_PARA_DETALHE_COMPLETO`__: nomes de classes para decompilação completa
- __`NAMESPACES_PARA_INCLUIR` / `NAMESPACES_PARA_IGNORAR`__: filtros de escopo
- __`MAXIMO_TOKENS_APROXIMADO`__: guarda de tamanho para evitar saídas gigantes

## Dicas e solução de problemas

- __Sem comentários `//IL_...`__: já limpamos automaticamente; caso deseje outros filtros, adapte `CleanDecompiledCode()`
- __Assemblies com dependências__: o decompilador foi configurado para não falhar na resolução; ainda assim, copie dependências junto do alvo quando possível
- __Unidades muito grandes__: ajuste filtros de namespace e a lista de classes detalhadas

## Ética e legalidade

Este projeto é para fins educacionais e de pesquisa. Respeite licenças e leis locais sobre engenharia reversa.

## Licença

MIT

---

# Knowledge Base Generator for AI (dnSpy-style) — EN

A tool to decompile .NET assemblies (e.g., Unity’s `Assembly-CSharp.dll`) and produce a dnSpy-like, readable knowledge base. Geared towards game code exploration and LLM dataset preparation.

## Highlights

- __On-demand detail__: full, dnSpy-like decompilation for selected classes
- __Optimized parallelization__: fast analysis for the rest (fields, properties, events, ctors, methods)
- __Structured output__: `namespace → type → members`, consistent indentation
- __Console UX__: progress bar and colored steps ([1/3], [2/3], [3/3])
- __Post-processing__: removes noisy IL comments (`//IL_...`)
- __Separate index__: lists all types and members

## Outputs

- `[AssemblyName]-knowledge-base-index.txt`
- `[AssemblyName]-knowledge-base-completo.txt`

## Requirements

- .NET SDK 9.0+

## Usage

1) Build: `dotnet build`
2) Drag and drop your `Assembly-CSharp.dll` onto the built executable
3) Open the generated `.txt` files next to the DLL

CLI (PowerShell):

```powershell
./bin/Debug/net9.0/ConsoleApp2.exe "C:\\Path\\To\\Assembly-CSharp.dll"
```

## Configuration (`Program.cs`)

- `CLASSES_PARA_DETALHE_COMPLETO`
- `NAMESPACES_PARA_INCLUIR` / `NAMESPACES_PARA_IGNORAR`
- `MAXIMO_TOKENS_APROXIMADO`

## Notes

- Educational/research use. Respect licenses and local laws.

## License

MIT
