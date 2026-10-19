# Локальный Postgres без Docker: портативные бинарники EDB в C:\Projects\salembonus-db
# (pgsql\bin — сервер, data — кластер, порт 5433, пользователь/пароль/база salembonus).
# Использование: scripts\db-local.ps1 start | stop | status
param([ValidateSet('start', 'stop', 'status')][string]$Action = 'start')
$root = 'C:\Projects\salembonus-db'
& "$root\pgsql\bin\pg_ctl.exe" -D "$root\data" -l "$root\pg.log" $Action
