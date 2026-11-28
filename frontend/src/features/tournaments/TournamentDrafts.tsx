import { TournamentPublication } from './TournamentPublication'
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { listGames, type Game } from '../games/api'
import { AuthenticationError } from '../identity/api'
import { tournamentRequest, TournamentPermissionError, type Tournament } from './api'
import { TournamentRulesEditor } from './TournamentRulesEditor'
import { TournamentParticipants } from './Registrations'
import { TournamentStart } from './TournamentStart'
import { TournamentMatches } from './TournamentMatches'
import { TournamentByes } from './TournamentByes'
import { SwissRounds } from './SwissRounds'
import { Standings } from './Standings'
import { TournamentLifecycle } from './TournamentLifecycle'

export function TournamentDrafts({ onAccessLost }: { onAccessLost: () => void }) {
  const [games, setGames] = useState<Game[]>([])
  const [tournaments, setTournaments] = useState<Tournament[]>([])
  const [selected, setSelected] = useState<Tournament | null>(null)
  const [name, setName] = useState('')
  const [gameId, setGameId] = useState('')
  const [filter, setFilter] = useState('')
  const [reload, setReload] = useState(0)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const mounted = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])

  const fail = useCallback((cause: unknown) => {
    if (!mounted.current) return
    if (cause instanceof AuthenticationError || cause instanceof TournamentPermissionError) { onAccessLost(); return }
    setError(cause instanceof Error ? cause.message : 'No se pudo cargar la lista de torneos.')
  }, [onAccessLost])

  useEffect(() => {
    let active = true
    setLoading(true); setError('')
    Promise.all([
      listGames(), tournamentRequest<Tournament[]>(filter ? `?gameId=${encodeURIComponent(filter)}` : ''),
    ]).then(([catalog, drafts]) => {
      if (active) { setGames(catalog); setTournaments(drafts) }
    }).catch(cause => { if (active) fail(cause) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [filter, reload, fail])

  async function create(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      const result = await tournamentRequest<Tournament>('', { name, gameId })
      if (mounted.current) {
        setSelected(result); setName(''); setGameId(''); setFilter(''); setReload(value => value + 1)
      }
    } catch (cause) { fail(cause) }
    finally { if (mounted.current) setBusy(false) }
  }

  async function open(id: string) {
    setBusy(true); setError(''); setSelected(null)
    try {
      const result = await tournamentRequest<Tournament>(`/${id}`)
      if (mounted.current) setSelected(result)
    } catch (cause) { fail(cause) }
    finally { if (mounted.current) setBusy(false) }
  }

  return <section className="tournaments-section" aria-labelledby="tournaments-heading">
    <div className="section-heading"><div>
      <h2 id="tournaments-heading">Mis torneos</h2><p>Borradores privados: solo vos podés consultarlos.</p>
    </div><button type="button" disabled={busy || loading} onClick={() => setReload(value => value + 1)}>Recargar torneos</button></div>
    {error && <p role="alert" className="form-alert form-alert--error">{error}</p>}
    <div className="tournaments-layout">
      <div>
        <h3>Crear torneo</h3>
        <form className="game-form" onSubmit={event => void create(event)}>
          <label htmlFor="tournament-name">Nombre del torneo</label>
          <input id="tournament-name" value={name} disabled={busy} required onChange={event => setName(event.target.value)} />
          <label htmlFor="tournament-game">Juego del torneo</label>
          <select id="tournament-game" value={gameId} disabled={busy} required onChange={event => setGameId(event.target.value)}>
            <option value="">Elegí un juego</option>{games.map(game => <option key={game.id} value={game.id}>{game.name}</option>)}
          </select>
          <p>El torneo se creará en estado borrador.</p>
          <button type="submit" disabled={busy || loading}>{busy ? 'Procesando…' : 'Crear torneo'}</button>
        </form>
      </div>
      <div>
        <label htmlFor="tournament-filter">Filtrar torneos por juego</label>
        <select id="tournament-filter" value={filter} disabled={busy} onChange={event => { setFilter(event.target.value); setSelected(null) }}>
          <option value="">Todos los juegos</option>{games.map(game => <option key={game.id} value={game.id}>{game.name}</option>)}
        </select>
        {loading ? <p role="status">Cargando torneos…</p> : tournaments.length === 0 ? <p>No hay torneos para este filtro.</p> :
          <ul className="tournament-list">{tournaments.map(tournament => <li key={tournament.id}>
            <button type="button" disabled={busy} onClick={() => void open(tournament.id)}>{tournament.name}</button>
            <span>{tournament.status === 'Draft' ? 'Borrador' : tournament.status === 'Finished' ? 'Finalizado' : tournament.status === 'Correction' ? 'En corrección' : 'En curso'} · {games.find(game => game.id === tournament.gameId)?.name}</span>
          </li>)}</ul>}
        {selected && <article className="tournament-detail" aria-labelledby="tournament-detail-heading">
          <h3 id="tournament-detail-heading">Detalle del torneo</h3>
          <dl><dt>Nombre</dt><dd>{selected.name}</dd><dt>Juego</dt><dd>{games.find(game => game.id === selected.gameId)?.name}</dd>
            <dt>Estado</dt><dd>{selected.status === 'Draft' ? 'Borrador' : selected.status}</dd><dt>Visibilidad</dt><dd>{selected.publishedAt ? 'Publicado' : 'Privado'}</dd></dl>
          <button disabled={busy} onClick={() => void open(selected.id)}>Recargar detalle del torneo</button>
          <TournamentStart key={`start-${selected.id}-${selected.status}`} tournament={selected} onAccessLost={onAccessLost} onSaved={value => { setSelected(value); setReload(n => n + 1) }} />
          <TournamentRulesEditor key={`${selected.id}-${selected.rulesVersion}-${selected.status}`} tournament={{ ...selected, rules: selected.startedRules ?? selected.rules }} onSaved={setSelected} onAccessLost={onAccessLost} />
          <TournamentLifecycle key={`lifecycle-${selected.id}-${selected.status}`} tournament={selected} onAccessLost={onAccessLost} onSaved={value => { setSelected(value); setReload(n => n + 1) }} />
          <TournamentPublication key={`publication-${selected.id}-${selected.status}-${selected.publicationVersion}`} tournament={selected} onSaved={value => { setSelected(value); setReload(n => n + 1) }} onAccessLost={onAccessLost} />
          <TournamentParticipants key={`participants-${selected.id}-${selected.status}`} tournamentId={selected.id} status={selected.status} onAccessLost={onAccessLost} />
          {selected.status !== 'Draft' && <TournamentMatches key={`matches-${selected.id}-${selected.status}`} status={selected.status} tournamentId={selected.id} onAccessLost={onAccessLost} />}
          {selected.status !== 'Draft' && <TournamentByes status={selected.status} key={`byes-${selected.id}`} tournamentId={selected.id} onAccessLost={onAccessLost} />}
          {selected.status !== 'Draft' && <SwissRounds status={selected.status} key={`rounds-${selected.id}`} tournamentId={selected.id} onAccessLost={onAccessLost} />}
          {selected.status !== 'Draft' && <Standings key={`standings-${selected.id}`} tournamentId={selected.id} onAccessLost={onAccessLost} />}
        </article>}
      </div>
    </div>
  </section>
}
