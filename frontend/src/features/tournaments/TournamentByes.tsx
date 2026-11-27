import { useEffect, useRef, useState } from 'react'
import { AuthenticationError } from '../identity/api'
import { tournamentRequest, TournamentPermissionError } from './api'

type Bye = { id: string; round: number; playerId: string; points: number; actorPlayerId: string; assignedAt: string }
type Player = { playerId: string; displayName: string }

export function TournamentByes({ tournamentId, onAccessLost }: { tournamentId: string; onAccessLost: () => void }) {
  const [byes, setByes] = useState<Bye[]>([])
  const [players, setPlayers] = useState<Player[]>([])
  const [round, setRound] = useState('1')
  const [player, setPlayer] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const mounted = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  const path = `/${tournamentId}/byes`
  const name = (id: string) => players.find(p => p.playerId === id)?.displayName ?? id
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
      tournamentRequest<Bye[]>(path), tournamentRequest<Player[]>(`/${tournamentId}/participants`),
    ])
    if (mounted.current) { setByes(list); setPlayers(participants) }
  }
  return <section aria-label="Byes">
    <h4>Byes</h4>
    <p>Un bye por ronda, solo con cantidad impar de inscriptos; máximo uno por jugador en el torneo. Otorga los puntos históricos sin contar como enfrentamiento.</p>
    <p>Los torneos iniciados con la política anterior conservan sus reglas sin asignación de byes. Consultá antes de asignar; la asignación es definitiva.</p>
    {error && <p role="alert">{error}</p>}
    <fieldset disabled={busy}>
      <button onClick={() => void run(refresh)}>Consultar byes</button>
      <form onSubmit={event => { event.preventDefault(); void run(async () => {
        await tournamentRequest<Bye>(path, { round: Number(round), playerId: player })
        if (mounted.current) setPlayer('')
        await refresh()
      }) }}>
        <label>Ronda del bye<input type="number" min="1" step="1" required value={round} onChange={e => setRound(e.target.value)} /></label>
        <label>Jugador del bye<select required value={player} onChange={e => setPlayer(e.target.value)}>
          <option value="">Elegí jugador</option>{players.map(p => <option key={p.playerId} value={p.playerId}>{p.displayName}</option>)}
        </select></label>
        <button disabled={!player}>Asignar bye</button>
      </form>
      <ul>{byes.map(b => <li key={b.id}>Ronda {b.round}: {name(b.playerId)} · Bye · {b.points} puntos
        <p>Asignó {name(b.actorPlayerId)} · {new Date(b.assignedAt).toLocaleString()}</p>
      </li>)}</ul>
    </fieldset>
  </section>
}
