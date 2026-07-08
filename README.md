# weft

version control for blender projects.

every change to an asset becomes an immutable, content-hashed snapshot with a
semantic diff. roll back in one click — every render remembers which snapshot
it came from.

## stack

- .NET 10 minimal apis + EF Core (postgres)
- react + typescript + vite

## run it (dev)

docker compose --profile dev up -d db
cd src/Weft.Api && dotnet run
cd client && npm install && npm run dev
