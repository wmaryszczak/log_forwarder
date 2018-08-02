
## Dry run

```
dotnet run -- -p /var/log/resfinity/wheels/ -f .complete -s scripts/wheels_gcs.cs.txt -b dry -v
```

## GCS run

GOOGLE_APPLICATION_CREDENTIALS environment variable must be provided

```
dotnet run -- -p /var/log/resfinity/wheels/ -f .complete -s scripts/wheels_gcs.cs.txt -b gcs -v
```