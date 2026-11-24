#!/usr/bin/env python3
"""Validación estructural de archivos y dependencias. No reemplaza dotnet build/test."""
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BACKEND = ROOT / 'backend'
REQUIRED = [
    'AGENTS.md', 'README.md', 'docs/ROADMAP.md', 'docs/TOURNAMENT_RULES.md',
    'docs/ARCHITECTURE.md', 'compose.yaml', 'TournamentManager.slnx',
    'backend/TournamentManager.Domain/TournamentManager.Domain.csproj',
    'backend/TournamentManager.Application/TournamentManager.Application.csproj',
    'backend/TournamentManager.Application/Abstractions/IDatabaseReadinessProbe.cs',
    'backend/TournamentManager.Infrastructure/TournamentManager.Infrastructure.csproj',
    'backend/TournamentManager.Infrastructure/DependencyInjection.cs',
    'backend/TournamentManager.Infrastructure/Health/PostgresReadinessProbe.cs',
    'backend/TournamentManager.Infrastructure/Persistence/TournamentDbContext.cs',
    'backend/TournamentManager.Infrastructure/Persistence/Migrations/20261122000000_InitialBaseline.cs',
    'backend/TournamentManager.Infrastructure/Persistence/Migrations/TournamentDbContextModelSnapshot.cs',
    'backend/TournamentManager.Api/TournamentManager.Api.csproj',
    'backend/TournamentManager.Api/Program.cs',
    'backend/TournamentManager.Api/Features/System/SystemEndpoints.cs',
    'backend/TournamentManager.Tests/HealthEndpointsTests.cs',
    'backend/TournamentManager.Tests/ArchitectureDependenciesTests.cs',
    'backend/TournamentManager.Tests/GamesDomainTests.cs',
    'backend/TournamentManager.Tests/GameCatalogTests.cs',
    'backend/TournamentManager.Tests/PostgresGameCatalogIntegrationTests.cs',
    'backend/TournamentManager.Domain/Games/Game.cs',
    'backend/TournamentManager.Domain/Games/GameFaction.cs',
    'backend/TournamentManager.Application/Features/Games/IGameCatalog.cs',
    'backend/TournamentManager.Application/Features/Games/CreateGame/CreateGame.cs',
    'backend/TournamentManager.Application/Features/Games/ListGames/ListGames.cs',
    'backend/TournamentManager.Application/Features/Games/GetGame/GetGame.cs',
    'backend/TournamentManager.Infrastructure/Games/EfGameCatalog.cs',
    'backend/TournamentManager.Infrastructure/Persistence/Migrations/20261122220000_AddGamesCatalog.cs',
    'backend/TournamentManager.Api/Features/Games/GameEndpoints.cs',
    'frontend/package.json', 'frontend/src/App.tsx', 'frontend/src/App.test.tsx',
    'frontend/src/features/games/GamesCatalog.tsx',
    'frontend/src/features/games/GamesCatalog.test.tsx',
    'frontend/src/features/games/api.ts',
    'docs/slices/01-CATALOGO.md',
    'docs/slices/02-IDENTIDAD.md',
    'backend/TournamentManager.Domain/Players/Player.cs',
    'backend/TournamentManager.Application/Features/Identity/IdentityContracts.cs',
    'backend/TournamentManager.Infrastructure/Identity/AccountAccess.cs',
    'backend/TournamentManager.Api/Features/Identity/IdentityEndpoints.cs',
    'frontend/src/features/identity/AccountPanel.tsx',
    '.github/workflows/ci.yml',
]

missing = [path for path in REQUIRED if not (ROOT / path).is_file()]
assert not missing, f'Archivos faltantes: {missing}'

package = json.loads((ROOT / 'frontend/package.json').read_text(encoding='utf-8'))
assert {'dev', 'test', 'build'} <= set(package['scripts']), 'Scripts npm faltantes'

# Explicitly validate allowed .NET project dependency arrows.
EXPECTED_PROJECT_REFS = {
    'TournamentManager.Domain': set(),
    'TournamentManager.Application': {'TournamentManager.Domain'},
    'TournamentManager.Infrastructure': {'TournamentManager.Domain', 'TournamentManager.Application'},
    'TournamentManager.Api': {'TournamentManager.Application', 'TournamentManager.Infrastructure'},
    'TournamentManager.Tests': {'TournamentManager.Api'},
}
for project, expected in EXPECTED_PROJECT_REFS.items():
    path = BACKEND / project / f'{project}.csproj'
    xml_root = ET.parse(path).getroot()
    refs = {
        Path(ref.attrib['Include'].replace('\\', '/')).parent.name
        for ref in xml_root.iter('ProjectReference')
    }
    assert refs == expected, f'{project}: referencias {refs}; esperadas {expected}'
    packages = {el.attrib['Include'] for el in xml_root.iter('PackageReference')}
    if project in ('TournamentManager.Domain', 'TournamentManager.Application'):
        assert not any(name.startswith(('Microsoft.EntityFrameworkCore', 'Npgsql', 'Microsoft.AspNetCore.Identity')) for name in packages), (
            f'{project}: dependencias EF/Npgsql prohibidas: {packages}'
        )
    if project == 'TournamentManager.Api':
        assert not any(name.startswith('Npgsql') for name in packages)
        assert not any(name.startswith('Microsoft.EntityFrameworkCore') and name != 'Microsoft.EntityFrameworkCore.Design' for name in packages)
    if project == 'TournamentManager.Infrastructure':
        assert 'Npgsql.EntityFrameworkCore.PostgreSQL' in packages

# C# source must not use EF or implementation-specific types outside infrastructure.
for project in ('TournamentManager.Domain', 'TournamentManager.Application'):
    for source in (BACKEND / project).rglob('*.cs'):
        source_lines = source.read_text(encoding='utf-8').splitlines()
        code = '\n'.join(line.split('//', 1)[0] for line in source_lines)
        forbidden = r'\b(Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore\.Identity|Npgsql|DbContext|DbSet|IQueryable|EntityEntry|SaveChangesAsync)\b'
        assert not re.search(forbidden, code), f'Persistencia fugada a {source}'
for source in (BACKEND / 'TournamentManager.Api' / 'Features').rglob('*.cs'):
    code = '\n'.join(line.split('//', 1)[0] for line in source.read_text(encoding='utf-8').splitlines())
    assert not re.search(r'\b(Microsoft\.EntityFrameworkCore|Npgsql|TournamentManager\.Infrastructure|DbContext|DbSet|IQueryable|EntityEntry|SaveChangesAsync)\b', code), (
        f'Endpoint acoplado a infraestructura: {source}'
    )

endpoints = (BACKEND / 'TournamentManager.Api/Features/System/SystemEndpoints.cs').read_text(encoding='utf-8')
assert '"/live"' in endpoints and '"/ready"' in endpoints
assert 'IDatabaseReadinessProbe' in endpoints
assert 'AddInfrastructure(' in (BACKEND / 'TournamentManager.Api/Program.cs').read_text(encoding='utf-8')
assert 'DbContext' in (BACKEND / 'TournamentManager.Infrastructure/Persistence/TournamentDbContext.cs').read_text(encoding='utf-8')

# Slice 01: implementación del catálogo con puerto y migración consistente.
assert 'IGameCatalog' in (BACKEND / 'TournamentManager.Application/Features/Games/CreateGame/CreateGame.cs').read_text(encoding='utf-8')
assert 'IGameCatalog' in (BACKEND / 'TournamentManager.Infrastructure/Games/EfGameCatalog.cs').read_text(encoding='utf-8')
assert 'MapGameEndpoints' in (BACKEND / 'TournamentManager.Api/Program.cs').read_text(encoding='utf-8')
assert 'UsePropertyAccessMode(PropertyAccessMode.Field)' in (BACKEND / 'TournamentManager.Infrastructure/Persistence/TournamentDbContext.cs').read_text(encoding='utf-8')
assert 'game_factions' in (BACKEND / 'TournamentManager.Infrastructure/Persistence/Migrations/20261122220000_AddGamesCatalog.cs').read_text(encoding='utf-8')
assert 'GamesCatalog' in (ROOT / 'frontend/src/App.tsx').read_text(encoding='utf-8')

print(f'OK: {len(REQUIRED)} archivos + estructura + referencias permitidas entre capas + ausencia de EF en Domain/Application/endpoints.')
print('AVISO: esta comprobación no compila C#, ni ejecuta tests .NET, Docker o npm.')
