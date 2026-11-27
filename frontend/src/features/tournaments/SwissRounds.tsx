import { useEffect, useRef, useState } from 'react'
import { AuthenticationError } from '../identity/api'
import { tournamentRequest, TournamentPermissionError } from './api'

type Round = { number: number; actorPlayerId: string; generatedAt: string; participantOrder: string[]; matches: { matchId: string; playerOneId: string; playerTwoId: string }[]; byePlayerId: string | null }
type State = { nextRound: number; enabled: boolean; rounds: Round[] }
type Player = { playerId: string; displayName: string }

export function SwissRounds({ tournamentId, onAccessLost }: { tournamentId: string; onAccessLost: () => void }) {
  const [state, setState] = useState<State | null>(null)
  const [players, setPlayers] = useState<Player[]>([])
  const [confirm, setConfirm] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const mounted = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  const path = `/${tournamentId}/rounds`
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
      tournamentRequest<State>(path), tournamentRequest<Player[]>(`/${tournamentId}/participants`),
    ])
    if (mounted.current) { setState(list); setPlayers(participants); setConfirm(false) }
  }
  return <section aria-label="Rondas suizas">
    <h4>Rondas suizas</h4>
    <p>La primera ronda se sortea. Las siguientes se arman desde los jugadores con menos puntos y resistencia, sin repetir rivales. El bye corresponde al peor clasificado elegible.</p>
    <p>Completá todos los resultados antes de avanzar. Los cruces guardados no se modifican al corregir un resultado.</p>
    {error && <p role="alert">{error}</p>}
    <fieldset disabled={busy}>
      <button onClick={() => void run(refresh)}>Consultar rondas</button>
      {state && (state.enabled ? <form onSubmit={event => { event.preventDefault(); void run(async () => {
        await tournamentRequest<Round>(path, { expectedRound: state.nextRound })
        await refresh()
      }) }}>
        <label><input type="checkbox" checked={confirm} onChange={e => setConfirm(e.target.checked)} />Confirmo generar la ronda {state.nextRound} completa</label>
        <button disabled={!confirm}>Generar ronda {state.nextRound}</button>
      </form> : <p>Este torneo conserva la modalidad manual histórica.</p>)}
      {state?.rounds.map(r => <article key={r.number} aria-label={`Ronda generada ${r.number}`}>
        <h5>Ronda {r.number}</h5>
        <ul>{r.matches.map(m => <li key={m.matchId}>{name(m.playerOneId)} vs {name(m.playerTwoId)}</li>)}
          {r.byePlayerId && <li>Bye: {name(r.byePlayerId)}</li>}
        </ul>
        <p>Generó {name(r.actorPlayerId)} · {new Date(r.generatedAt).toLocaleString()}</p>
        {r.number === 1 && <details><summary>Orden del sorteo</summary><ol>{r.participantOrder.map(id => <li key={id}>{name(id)}</li>)}</ol></details>}
      </article>)}
    </fieldset>
    <p>Consultá las partidas para cargar resultados y la tabla para ver la clasificación actualizada.</p>
  </section>
}
