import { useEffect, useRef, useState } from 'react'
import { AuthenticationError } from '../identity/api'
import { tournamentRequest, TournamentPermissionError } from './api'

type Result = { number: number; outcome: string; kind: string; reason: string; actorPlayerId: string; recordedAt: string; playerOnePoints: number; playerTwoPoints: number }
type Match = { id: string; round: number; playerOneId: string; playerTwoId: string; version: number; result: Result | null; history?: Result[] }
type Player = { playerId: string; displayName: string }

export function TournamentMatches({ tournamentId, onAccessLost }: { tournamentId: string; onAccessLost: () => void }) {
  const [players, setPlayers] = useState<Player[]>([])
  const [matches, setMatches] = useState<Match[]>([])
  const [selected, setSelected] = useState<Match | null>(null)
  const [round, setRound] = useState('1')
  const [one, setOne] = useState('')
  const [two, setTwo] = useState('')
  const [outcome, setOutcome] = useState('')
  const [kind, setKind] = useState('Played')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const mounted = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  const path = `/${tournamentId}/matches`
  const name = (id: string) => players.find(p => p.playerId === id)?.displayName ?? id
  function resultText(m: Match, r: Result) {
    return `${r.outcome === 'Draw' ? 'Empate' : `Ganó ${name(r.outcome === 'PlayerOneWin' ? m.playerOneId : m.playerTwoId)}`} · ${r.kind === 'Forfeit' ? 'Forfeit' : 'Jugada'} · ${r.playerOnePoints} / ${r.playerTwoPoints} puntos`
  }
  async function run(action: () => Promise<void>) {
    setBusy(true); setError('')
    try { await action() } catch (cause) {
      if (mounted.current) {
        if (cause instanceof AuthenticationError || cause instanceof TournamentPermissionError) onAccessLost()
        else setError(cause instanceof Error ? cause.message : 'No se pudo completar la operación.')
      }
    } finally { if (mounted.current) setBusy(false) }
  }
  async function refresh() {
    const [list, participants] = await Promise.all([
      tournamentRequest<Match[]>(path), tournamentRequest<Player[]>(`/${tournamentId}/participants`),
    ])
    if (mounted.current) { setMatches(list); setPlayers(participants) }
  }
  function select(match: Match) {
    setSelected(match); setOutcome(match.result?.outcome ?? ''); setKind(match.result?.kind ?? 'Played'); setReason('')
  }
  return <section aria-label="Partidas y resultados">
    <h4>Partidas y resultados</h4>
    <p>Creá enfrentamientos por ronda. Los puntos usan las reglas históricas; las correcciones conservan el historial.</p>
    {error && <p role="alert">{error}</p>}
    <fieldset disabled={busy}>
      <button onClick={() => void run(async () => { await refresh(); if (mounted.current) setSelected(null) })}>Consultar partidas</button>
      <form onSubmit={event => { event.preventDefault(); void run(async () => {
        const match = await tournamentRequest<Match>(path, { round: Number(round), playerOneId: one, playerTwoId: two })
        if (mounted.current) { select(match); setOne(''); setTwo('') }
        await refresh()
      }) }}>
        <label>Ronda<input type="number" min="1" step="1" required value={round} onChange={e => setRound(e.target.value)} /></label>
        <label>Jugador 1<select required value={one} onChange={e => setOne(e.target.value)}><option value="">Elegí jugador</option>{players.map(p => <option key={p.playerId} value={p.playerId}>{p.displayName}</option>)}</select></label>
        <label>Jugador 2<select required value={two} onChange={e => setTwo(e.target.value)}><option value="">Elegí jugador</option>{players.map(p => <option key={p.playerId} value={p.playerId}>{p.displayName}</option>)}</select></label>
        <button disabled={!one || !two}>Crear partida</button>
      </form>
      <ul>{matches.map(m => <li key={m.id}>
        <button onClick={() => void run(async () => {
          const detail = await tournamentRequest<Match>(`${path}/${m.id}`)
          if (mounted.current) select(detail)
        })}>Ronda {m.round}: {name(m.playerOneId)} vs {name(m.playerTwoId)}</button>
        <p>{m.result ? resultText(m, m.result) : 'Resultado pendiente'}</p>
      </li>)}</ul>
      {selected && <article aria-label="Resultado de partida">
        <h5>{name(selected.playerOneId)} vs {name(selected.playerTwoId)} · Ronda {selected.round}</h5>
        {selected.result && <p>{resultText(selected, selected.result)}</p>}
        <form onSubmit={event => { event.preventDefault(); void run(async () => {
          const updated = await tournamentRequest<Match>(`${path}/${selected.id}/result`, { outcome, kind, reason, expectedVersion: selected.version })
          if (mounted.current) select(updated)
          await refresh()
        }) }}>
          <label>Resultado<select required value={outcome} onChange={e => setOutcome(e.target.value)}>
            <option value="">Elegí resultado</option><option value="PlayerOneWin">Gana {name(selected.playerOneId)}</option><option value="PlayerTwoWin">Gana {name(selected.playerTwoId)}</option><option value="Draw">Empate</option>
          </select></label>
          <label>Tipo de resultado<select value={kind} onChange={e => setKind(e.target.value)}><option value="Played">Partida jugada</option><option value="Forfeit">Forfeit / abandono de esta partida</option></select></label>
          <label>Motivo de la corrección o nota<input required={selected.version > 0} maxLength={500} value={reason} onChange={e => setReason(e.target.value)} /></label>
          <button disabled={!outcome || (selected.version > 0 && !reason.trim())}>{selected.version > 0 ? 'Corregir resultado' : 'Guardar resultado'}</button>
        </form>
        {selected.history?.map(r => <details key={r.number}><summary>Versión {r.number} · {r.reason}</summary>
          <p>{resultText(selected, r)}</p><p>Registró {name(r.actorPlayerId)} · {new Date(r.recordedAt).toLocaleString()}</p>
        </details>)}
      </article>}
    </fieldset>
  </section>
}
