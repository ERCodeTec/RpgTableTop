## Nova funcionalidade
Implementação do sistema de [Tamanho] dos [Tokens] e [Personagens] 

## Comportamento esperado
- No Sistema de Grid, as fichas de token possuam tamanhos diferentes, baseados na categoria de tamanho informado em suas respectivas fichas.
- Isto serve para todos os tipos de tokens.

## Categorias de Tamanho e Espaço no Mapa
- `Minúsculo`: Ocupa 0,75 x 0,75 m (espaço de 2,5 x 2,5 pés).
- `Pequeno`: Ocupa 1,5 x 1,5 m (espaço de 5 x 5 pés).
- `Médio`: Ocupa 1,5 x 1,5 m (espaço de 5 x 5 pés).
- `Grande`: Ocupa 3 x 3 m (espaço de 10 x 10 pés / 2x2 quadrados).
- `Enorme`: Ocupa 4,5 x 4,5 m (espaço de 15 x 15 pés / 3x3 quadrados).
- `Gargantuesco`: Ocupa 6 x 6 m ou mais (espaço de 20 x 20 pés ou mais / 4x4 quadrados ou maiores).

## Regras de convivência com o código atual
- Antes de planejar, leia o código existente relacionado e identifique os padrões em uso
  (estrutura de pastas, nomenclatura, tratamento de erros, validação, testes) e siga-os.
- Reaproveite componentes, helpers e serviços existentes em vez de criar duplicatas.
- Mudanças em código existente devem ser mínimas e justificadas. Sem refatorações não pedidas.
- Não adicionar dependências novas sem me consultar.
- Mudanças no banco devem ser via migration, retrocompatíveis e reversíveis.
- Não quebrar contratos existentes (API pública, tipos exportados, eventos).

## Fora de escopo
- Nenhuma Alteração nos grids caso achar necessário devido a movimentação. 

## Critérios de aceite
- Todos os `Tokens` devem começar com o Tamnho `Médio`
- Todos os `tokens` devem ocupar o espaço pertinente ao seu tamanho.
- Um `token` não pode passar por blocos se o seu tamanho não permitir.

## Entregável desta etapa (SOMENTE planejamento, sem alterar código)
1. O que você encontrou no código atual que é relevante (arquivos, padrões, pontos de integração).
2. Perguntas e ambiguidades que dependem de decisão minha.
3. Plano em etapas pequenas e ordenadas. Para cada etapa: arquivos criados/alterados, o que muda
   e como validar.
4. Análise de impacto: o que pode quebrar, quem consome o que será alterado, migrações necessárias.
5. Riscos e alternativas, com recomendação.
6. Estratégia de testes e de rollback (incluindo feature flag, se fizer sentido).

Aguarde minha aprovação antes de implementar.
