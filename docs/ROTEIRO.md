# Roteiro de deploy da Mesa Virtual (vtt.ercode.tech)

Guia prático com os comandos para atualizar, conferir, voltar versão e fazer backup da Mesa na VPS.
Como a instalação foi montada (DNS, deploy key, `.env`, proxy) fica no [deploy-vps.md](deploy-vps.md).

**Convenções:**
- **🖥️ VPS**: rodar na VPS como `deploy`, dentro de `/opt/vtt`.
- **💻 Mac**: rodar no seu computador.

**Regra de ouro:** na VPS, sempre dentro de `/opt/vtt`. O `.env` de lá tem
`COMPOSE_FILE=docker-compose.yml:docker-compose.vps.yml`, então todo `docker compose ...` já usa
os ajustes da VPS (sem portas publicadas, rede do proxy). Fora dessa pasta, o compose não funciona.

---

## 0. Entrar na VPS (sempre o primeiro passo)

🖥️

```bash
ssh erp-vps
cd /opt/vtt
```

---

## 1. 🔄 Atualizar a Mesa (deploy de versão nova)

A Mesa é uma instância só (não há clientes como no ERP). A atualização gera a imagem nova e troca
o container da API; o banco e as imagens enviadas ficam nos volumes e não são tocados. A mesa fica
fora do ar por poucos segundos na troca, e quem estiver jogando reconecta sozinho.

### 1.1 Publicar o código (💻 Mac)

Commit e push na `main`. Confira que o GitHub está em dia:

```bash
git status -sb        # deve mostrar "## main...origin/main", sem [ahead N]
```

### 1.2 Ver o que vem na versão nova (🖥️ VPS)

```bash
git fetch
git log --oneline HEAD..origin/main                     # commits que vão entrar
git diff --stat HEAD origin/main -- .env.example docker-compose.yml docker-compose.vps.yml Dockerfile
git diff --stat HEAD origin/main -- backend/src/Vtt.Infrastructure/Persistence/Migrations
```

- **Mudou o `.env.example`?** Veja o que mudou (`git diff HEAD origin/main -- .env.example`) e,
  se apareceu variável nova, acrescente no `.env` da VPS (`nano .env`) **antes** do passo 1.5.
- **Tem migration nova?** Ela roda sozinha quando a API sobe (`DATABASE_AUTO_MIGRATE=true`).
  Só não pule o backup do passo 1.3.

Se `git log` não mostrar nada, a VPS já está na versão mais nova.

### 1.3 Backup antes de atualizar (🖥️ VPS)

Banco + imagens enviadas, sem parar a mesa:

```bash
./scripts/backup.sh
```

Termina com `Backup concluído:` e os dois arquivos em `backups/`. Se aparecer `ERRO`, **pare aqui**.

### 1.4 Guardar a versão atual (🖥️ VPS)

Uma etiqueta na imagem que está rodando, para poder voltar em segundos (seção 2):

```bash
git rev-parse --short HEAD                 # anote: é a versão que está no ar
docker tag vtt-api:latest vtt-api:anterior
```

### 1.5 Atualizar (🖥️ VPS)

```bash
git pull --ff-only
docker compose up -d --build     # alguns minutos: npm ci + build do front + build .NET em 1 vCPU
```

O build roda com a versão antiga ainda no ar; ela só cai na troca do container, no final.

### 1.6 Conferir (🖥️ VPS)

```bash
docker compose ps                # api "healthy" (pode passar uns 40 s em "starting")
docker compose logs --tail 50 api
curl -s https://vtt.ercode.tech/health     # deve responder Healthy
```

Se houve migration, confira que ela entrou (a mais nova aparece primeiro):

```bash
docker compose exec postgres psql -U vtt -d vtt -c 'SELECT * FROM "__EFMigrationsHistory" ORDER BY 1 DESC LIMIT 3;'
```

Depois abra **https://vtt.ercode.tech** e teste o que mudou. Vale sempre um teste rápido de entrar
numa mesa e mover um token (confirma o SignalR pelo proxy).

### 1.7 Limpar imagens antigas (🖥️ VPS)

Só depois de conferir que está tudo bem:

```bash
docker image prune -f            # apaga só as sobras sem nome; a vtt-api:anterior fica
```

---

## 2. ⏪ Voltar para a versão anterior (deu ruim depois de atualizar)

🖥️ VPS — usa a etiqueta do passo 1.4, sem build (instantâneo):

```bash
docker tag vtt-api:anterior vtt-api:latest
docker compose up -d api
docker compose ps
```

⚠️ A pasta `/opt/vtt` continua com o código novo: o próximo `docker compose up -d --build`
gera a versão nova de novo. Corrija o problema no Mac, faça push e atualize normalmente (seção 1).

⚠️ **Se a versão nova tinha migration**, o banco já está no formato novo. Migrations que só
acrescentam coluna opcional (o caso mais comum) funcionam com a versão antiga. Se a versão antiga
der erro de banco, restaure o backup do passo 1.3 (seção 3).

Se a etiqueta `anterior` não existir (passo 1.4 pulado), volte pelo código:

```bash
git log --oneline -10                     # copie o código da versão que funcionava
git checkout <codigo>
docker compose up -d --build
git checkout main                         # volta a pasta para a main (a imagem antiga segue no ar)
```

---

## 3. 💾 Backup e restauração

🖥️ VPS:

```bash
./scripts/backup.sh              # banco + imagens -> backups/ (com data e hora no nome)
ls -lh backups/
```

O backup automático (03:30, com limpeza de 14 dias) está descrito no
[deploy-vps.md, seção 7](deploy-vps.md#7-backup-automático). Conferir se está rodando:

```bash
crontab -l | grep vtt
tail -20 /home/deploy/backup-vtt.log
```

### Copiar os backups para o Mac (💻, faça com frequência!)

```bash
mkdir -p ~/Backups/vtt
scp -r erp-vps:/opt/vtt/backups/ ~/Backups/vtt/
```

### Restaurar (⚠️ substitui o banco e as imagens atuais)

🖥️ VPS — use o **par** de arquivos do mesmo horário:

```bash
./scripts/backup.sh                                    # estado atual, por segurança
./scripts/restaurar.sh backups/vtt-banco-AAAA-MM-DD_HHMMSS.dump backups/vtt-uploads-AAAA-MM-DD_HHMMSS.tar.gz
```

O script pede para digitar `restaurar`, para a API, restaura e sobe de novo. A mensagem final
cita `localhost:8080` (vale para o Mac); na VPS confira com `docker compose ps`.

---

## 4. 🛠️ Dia a dia

🖥️ VPS, em `/opt/vtt`:

| Para quê | Comando |
|---|---|
| Versão no ar (código do commit) | `git log --oneline -1` |
| Ver se está no ar | `docker compose ps` |
| Logs da API (ao vivo) | `docker compose logs -f api` (Ctrl+C para sair) |
| Últimas 100 linhas de log | `docker compose logs --tail 100 api` |
| Logs do banco | `docker compose logs -f postgres` |
| Reiniciar a API | `docker compose restart api` |
| Parar a Mesa (sem apagar nada) | `docker compose stop` |
| Ligar de novo | `docker compose up -d` |
| Abrir o banco (sair com `\q`) | `docker compose exec postgres psql -U vtt -d vtt` |
| Saúde (API + banco) | `curl -s https://vtt.ercode.tech/health` |
| Memória e CPU dos containers | `docker stats --no-stream` |
| Memória livre / disco | `free -h` / `df -h /` |

⚠️ **Nunca** rode `docker compose down -v`: o `-v` apaga o banco e as imagens enviadas.

Para abrir o banco pelo VS Code no Mac, use o atalho `bancos` (porta `15433`, container
`vtt-postgres-1`; usuário e banco `vtt`), descrito no ROTEIRO do ERP, seção 4. A senha está no
`.env` da VPS: `ssh erp-vps 'grep POSTGRES_PASSWORD /opt/vtt/.env'`.

---

## 5. 🚨 Problemas comuns

| O que aparece | O que fazer |
|---|---|
| `git pull` falha com "not possible to fast-forward" | Alguém editou arquivo direto na VPS: veja `git status` antes de mexer |
| `Defina POSTGRES_PASSWORD no arquivo .env` | Rodou o compose fora de `/opt/vtt` (ou o `.env` sumiu) |
| `network proxy declared as external, but could not be found` | Proxy central fora do ar: `cd /opt/infra/proxy && docker compose up -d` |
| **502** no navegador | API ainda subindo/migrando ou caiu: `docker compose ps` e `logs api` |
| API em `unhealthy` ou reiniciando sem parar | Veja `docker compose logs --tail 100 api`; erro de migration → volte (seção 2) e restaure se preciso (seção 3) |
| Build muito lento ou travando | Pouca memória: `free -h` e `docker stats --no-stream` (o build é o momento mais pesado) |
| `No such image: vtt-api:anterior` | O passo 1.4 não foi feito: volte pelo código (fim da seção 2) |

Mais casos no [deploy-vps.md](deploy-vps.md#problemas-comuns).

---

## ⚡ Cola rápida

```
ATUALIZAR:  Mac: push na main
            VPS: ssh erp-vps → cd /opt/vtt → git fetch → git log --oneline HEAD..origin/main
                 (.env.example mudou? → nano .env)
                 → ./scripts/backup.sh → docker tag vtt-api:latest vtt-api:anterior
                 → git pull --ff-only → docker compose up -d --build
                 → docker compose ps → curl -s https://vtt.ercode.tech/health → docker image prune -f

VOLTAR:     docker tag vtt-api:anterior vtt-api:latest → docker compose up -d api

BACKUP:     ./scripts/backup.sh   |   Mac: scp -r erp-vps:/opt/vtt/backups/ ~/Backups/vtt/
```
