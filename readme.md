
## Dry run

```
ASPNETCORE_URLS=http://0.0.0.0:5002 dotnet run -- -p /var/log/resfinity/wheels/ -f .complete -s scripts/wheels_gcs.cs.txt -b gcs --dry -w 4
```

## Dry run for single file mode

```
ASPNETCORE_URLS=http://0.0.0.0:5002 dotnet run -- -p /var/log/resfinity/wheels/ -f "*.log" -b gcs --dry -w 4 -m single_file
```

## GCS run

GOOGLE_APPLICATION_CREDENTIALS environment variable must be provided

```
ASPNETCORE_URLS=http://0.0.0.0:5002 dotnet run -- -p /var/log/resfinity/wheels/ -f .complete -s scripts/wheels_gcs.cs.txt -b gcs -w 4
```


## GCS run for single file mode

GOOGLE_APPLICATION_CREDENTIALS environment variable must be provided

```
ASPNETCORE_URLS=http://0.0.0.0:5002 dotnet run -- -p /var/log/resfinity/wheels/ -f "*.log" -b gcs -w 4 -m single_file
```

## Health check

```
curl localhost:5002/health
```

## Touch all logs to be send

```
cd /var/log/whatever_the_place_for_logs
find . ! -path . -type d -exec touch {}/.complete \;
```

## How to deploy

```
USERNAME=<taret_machine_user> IP=<target_machine_ip> bundle exec cap acceptance deploy
```


export GOOGLE_APPLICATION_CREDENTIALS=/src/log_forwarder/LogForwarder.App/anixe-extra.json
