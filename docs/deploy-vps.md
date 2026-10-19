# Deploy na VPS da Hostinger (vtt.ercode.tech)

A Mesa Virtual roda na VPS da ERCode (`187.127.63.107`), atrás do **proxy central** (Caddy),
que emite o HTTPS de todos os subdomínios de `ercode.tech`. O servidor e o proxy são descritos no
repositório **`ERCodeTec/ercode-infra`**; este guia cobre só a Mesa.

```
Internet ──443──> proxy central (ercode-infra, /opt/infra/proxy)
                    ├── ... outros produtos
                    └── vtt.ercode.tech ──> vtt-web (API .NET: front React + SignalR + uploads)
                                              └──> postgres (PostgreSQL 17, sem porta exposta)
```

O Node só é usado **durante o build** (gera o front com Vite dentro do Dockerfile); em produção
roda um único container .NET. Na VPS, o arquivo `docker-compose.vps.yml` complementa o
`docker-compose.yml`: remove as portas publicadas e liga a API à rede `proxy`.

> ⚠️ Nunca publique a porta 8080 na VPS: o Docker passa por cima do firewall (ufw) e a API
> ficaria exposta sem HTTPS — e ela confia nos cabeçalhos `X-Forwarded-*` de qualquer origem.

Pré-requisitos (guia do `ercode-infra`): usuário `deploy`, firewall, proxy central no ar.

---

## 1. DNS

O registro curinga `*.ercode.tech` já aponta para a VPS, então não há nada a fazer.
Confira no Mac: `dig +short vtt.ercode.tech` deve devolver `187.127.63.107`.

## 2. Deploy key do GitHub (repositório privado)

Cada repositório precisa da sua própria deploy key. **Na VPS** (`ssh erp-vps`):

```bash
ssh-keygen -t ed25519 -C "deploy-vps-vtt" -f ~/.ssh/github_vtt -N ""
cat >> ~/.ssh/config <<'EOF'

Host github-vtt
  HostName github.com
  IdentityFile ~/.ssh/github_vtt
  IdentitiesOnly yes
EOF
cat ~/.ssh/github_vtt.pub
```

Cadastre a chave em GitHub → `ERCodeTec/RpgTableTop` → *Settings* → *Deploy keys* → *Add deploy key*
(sem permissão de escrita). Ou, no Mac, com o `gh`:

```bash
ssh erp-vps 'cat ~/.ssh/github_vtt.pub' > /tmp/github_vtt.pub
gh repo deploy-key add /tmp/github_vtt.pub -R ERCodeTec/RpgTableTop -t "VPS Hostinger (somente leitura)"
```

## 3. Baixar o código

A pasta `/opt/vtt` já existe com dono `deploy` (preparação do servidor). Se não existir, crie
como root: `ssh ercode-vps 'mkdir -p /opt/vtt && chown deploy:deploy /opt/vtt'`.

```bash
git clone git@github-vtt:ERCodeTec/RpgTableTop.git /opt/vtt
```

## 4. Configurar o `.env`

```bash
cd /opt/vtt
cp .env.example .env
chmod 600 .env
nano .env
```

Valores para a VPS:

```dotenv
POSTGRES_PASSWORD=<resultado de: openssl rand -hex 24>
JWT_SECRET=<resultado de: openssl rand -base64 48>
JWT_ISSUER=vtt
JWT_EXPIRATION_MINUTES=720
PUBLIC_BASE_URL=https://vtt.ercode.tech
ASPNETCORE_ENVIRONMENT=Production
DATABASE_AUTO_MIGRATE=true

# Liga os ajustes da VPS (sem portas publicadas, rede do proxy)
COMPOSE_FILE=docker-compose.yml:docker-compose.vps.yml
```

Pode apagar ou deixar vazias as linhas de `NGROK_*`, `API_PORT`, `POSTGRES_PORT` e `NGROK_INSPECT_PORT`
— na VPS não são usadas (o HTTPS vem do proxy, não do ngrok).

Com o `COMPOSE_FILE` no `.env`, todo `docker compose ...` dentro de `/opt/vtt` já usa os dois arquivos.

## 5. Subir a Mesa

```bash
cd /opt/vtt
docker compose up -d --build     # 1ª vez demora (npm ci + build do front + build .NET em 1 vCPU)
docker compose ps                # api deve ficar "healthy" (as migrations rodam na subida)
docker compose logs -f api       # Ctrl+C para sair
```

Confira que **nenhuma porta** foi publicada (a coluna PORTS deve mostrar só `8080/tcp`, sem `0.0.0.0:`):

```bash
docker compose ps --format "{{.Service}}: {{.Ports}}"
```

## 6. Ligar o subdomínio no proxy central

No repositório `ercode-infra`, em `proxy/Caddyfile`, ative o bloco da Mesa (commit e push):

```
vtt.ercode.tech {
	reverse_proxy vtt-web:8080
}
```

E na VPS:

```bash
cd /opt/infra && git pull && cd proxy && docker compose restart caddy
docker compose logs --tail 20 caddy    # procure "certificate obtained successfully" para vtt.ercode.tech
```

Acesse **https://vtt.ercode.tech**. Teste também entrar numa mesa por um link de convite e
mover um token (confirma que o SignalR/WebSocket passa pelo proxy) e enviar uma imagem (uploads).

## 7. Backup automático

O repositório já tem `scripts/backup.sh` (banco + imagens enviadas). Agende no cron do `deploy`:

```bash
chmod +x /opt/vtt/scripts/backup.sh
/opt/vtt/scripts/backup.sh            # teste manual -> /opt/vtt/backups
crontab -e
# adicione (03:30, para não coincidir com outros backups da VPS — veja o ercode-infra):
30 3 * * * /opt/vtt/scripts/backup.sh >> /home/deploy/backup-vtt.log 2>&1
```

O script não apaga backups antigos. Para manter só 14 dias, adicione outra linha:

```
45 3 * * * find /opt/vtt/backups -type f -mtime +14 -delete
```

## 8. Atualizar (novos deploys)

```bash
cd /opt/vtt
git pull
docker compose up -d --build
docker image prune -f
```

As migrations rodam sozinhas na subida (`DATABASE_AUTO_MIGRATE=true`).

## Recursos da VPS

A VPS tem 1 vCPU e 4 GB de RAM (+2 GB de swap) compartilhada com outros produtos. Para uso normal é
suficiente; o momento mais pesado é o `--build`. Acompanhe com `docker stats --no-stream`
e `free -h`. Se a mesa ficar lenta em sessões com vários jogadores, considere subir para o plano KVM 2.

## Problemas comuns

| Sintoma | Causa provável |
|---------|----------------|
| `network proxy declared as external, but could not be found` | proxy central não está no ar (`cd /opt/infra/proxy && docker compose up -d`) |
| 502 em vtt.ercode.tech | API ainda subindo/migrando, ou caiu (`docker compose ps` e `logs api` em `/opt/vtt`) |
| Links de convite com `http://` ou endereço errado | `PUBLIC_BASE_URL` vazio ou errado no `.env` |
| Mesa não atualiza em tempo real | conexão do SignalR caindo — veja o console do navegador (F12) |
| Todos deslogados após trocar `JWT_SECRET` | esperado: tokens antigos deixam de valer |
| Erro de senha do banco após mudar `POSTGRES_PASSWORD` | a senha só vale na 1ª subida; o volume guarda a antiga |
