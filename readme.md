
## Dry run

```
dotnet run -- -p /var/log/resfinity/wheels/ -f .complete -s scripts/wheels_gcs.cs.txt -b dry -v
```

## GCS run

GOOGLE_APPLICATION_CREDENTIALS environment variable must be provided

```
dotnet run -- -p /var/log/resfinity/wheels/ -f .complete -s scripts/wheels_gcs.cs.txt -b gcs -v
```

## Touch all logs to be send

```
cd /var/log/whatever_the_place_for_logs
find . ! -path . -type d -exec touch {}/.complete \;
```