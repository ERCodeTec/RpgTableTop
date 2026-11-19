## Nova funcionalidade
Implementar o sistema de organização de Mapas e tokens por pasta

## Comportamento esperado
- Atualmente os mapas das mesas são criados todos no mesmo lugar, dificultando a organização e localização dos mápas pelo mestre na medida que o mapas forem sendo criados
- Esperado que o mestre na própria tela de criação/edição de mapas possa realizar o agrupamento destes mapas, sendo possível agrupalos por "Pastas" que ele pode pode personalizar. 

## Regras de convivência com o código atual
- Antes de planejar, leia o código existente relacionado e identifique os padrões em uso
  (estrutura de pastas, nomenclatura, tratamento de erros, validação, testes) e siga-os.
- Reaproveite componentes, helpers e serviços existentes em vez de criar duplicatas.
- Mudanças em código existente devem ser mínimas e justificadas. Sem refatorações não pedidas.
- Não adicionar dependências novas sem me consultar.
- Mudanças no banco devem ser via migration, retrocompatíveis e reversíveis.
- Não quebrar contratos existentes (API pública, tipos exportados, eventos).

## Fora de escopo
- Os Mapas e tokens já existentes não precisam ser agrupados em uma pasta.

## Critérios de aceite
- Não deve ser permitido criar "Pastas" com o mesmo nome!
- Não existe limitação de quantidade de mapas dentro de uma pasta.
- Se for necessário a visualização pode seguir a mesma estrutura de uma `tree view` não há necessidade de 
- A utilização das pastas não é obrigatório.

## Entregável desta etapa (SOMENTE planejamento, sem alterar código)
1. O que você encontrou no código atual que é relevante (arquivos, padrões, pontos de integração).
2. Perguntas e ambiguidades que dependem de decisão minha.
3. Plano em etapas pequenas e ordenadas. Para cada etapa: arquivos criados/alterados, o que muda
   e como validar.
4. Análise de impacto: o que pode quebrar, quem consome o que será alterado, migrações necessárias.
5. Riscos e alternativas, com recomendação.
6. Estratégia de testes e de rollback (incluindo feature flag, se fizer sentido).

Aguarde minha aprovação antes de implementar.
