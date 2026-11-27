import { useEffect, useRef, useState } from 'react'
import { AuthenticationError } from '../identity/api'
import { tournamentRequest, TournamentPermissionError } from './api'

type Round = { number: number; revision: number; reason: string; actorPlayerId: string; generatedAt: string; participantOrder: string[]; matches: { matchId: string; playerOneId: string; playerTwoId: string }[]; byePlayerId: string | null }
type State = { nextRound: number; enabled: boolean; rounds: Round[]; matchesVersion: number; resultsVersion: number; lifecycleVersion: number }
type History = { replacementReason: string; replacedByPlayerId: string; replacedAt: string; round: number; revision: number; matches: { id: string; playerOneId: string; playerTwoId: string; history: { number: number; reason: string; outcome: string; kind: string; recordedAt: string }[] }[]; byes: { id: string; playerId: string }[] }
type Player = { playerId: string; displayName: string }

export function SwissRounds({ tournamentId, status = 'InProgress', onAccessLost }: { tournamentId: string; status?: string; onAccessLost: () => void }) {
  const [state, setState] = useState<State | null>(null)
  const [players, setPlayers] = useState<Player[]>([])
  const [confirm, setConfirm] = useState(false)
  const [replace, setReplace] = useState(false)
  const [reason, setReason] = useState('')
  const [history, setHistory] = useState<History[]>([])
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
      {state && (state.enabled && status === 'InProgress' ? <form onSubmit={event => { event.preventDefault(); void run(async () => {
        if (replace) await tournamentRequest<Round>(`${path}/regenerate`, { round: state.nextRound - 1, reason,
          expectedMatchesVersion: state.matchesVersion, expectedResultsVersion: state.resultsVersion, expectedLifecycleVersion: state.lifecycleVersion })
        else await tournamentRequest<Round>(path, { expectedRound: state.nextRound })
        if (mounted.current) { setReplace(false); setReason(''); setHistory([]) }
        await refresh()
      }) }}>
        {state.nextRound > 1 && <label><input type="checkbox" checked={replace} onChange={e => { setReplace(e.target.checked); setConfirm(false) }} />Reemplazar únicamente la última ronda</label>}
        {replace && <><p>Se reemplazarán los cruces y el bye de la última ronda. Sus resultados quedarán anulados y conservados en el historial; los nuevos estarán pendientes.</p>
          <label>Motivo de regeneración<input required maxLength={500} value={reason} onChange={e => setReason(e.target.value)} /></label></>}
        <label><input type="checkbox" checked={confirm} onChange={e => setConfirm(e.target.checked)} />Confirmo generar la ronda {replace ? state.nextRound - 1 : state.nextRound} completa</label>
        <button disabled={!confirm || (replace && !reason.trim())}>Generar ronda {replace ? `${state.nextRound - 1} nuevamente` : state.nextRound}</button>
      </form> : <p>{status === 'Finished' || status === 'Correction' ? 'Los cruces quedan fijos después del cierre; la reapertura solo permite corregir resultados.' : 'Este torneo conserva la modalidad manual histórica.'}</p>)}
      {state?.rounds.map(r => <article key={r.number} aria-label={`Ronda generada ${r.number}`}>
        <h5>Ronda {r.number} · versión {r.revision ?? 1}</h5>
        {r.reason && <p>{r.reason}</p>}
        <ul>{r.matches.map(m => <li key={m.matchId}>{name(m.playerOneId)} vs {name(m.playerTwoId)}</li>)}
          {r.byePlayerId && <li>Bye: {name(r.byePlayerId)}</li>}
        </ul>
        <p>Generó {name(r.actorPlayerId)} · {new Date(r.generatedAt).toLocaleString()}</p>
        {r.number === 1 && <details><summary>Orden del sorteo</summary><ol>{r.participantOrder.map(id => <li key={id}>{name(id)}</li>)}</ol></details>}
      </article>)}
      <button onClick={() => void run(async () => {
        const rows = await tournamentRequest<History[]>(`${path}/history`)
        if (mounted.current) setHistory(rows)
      })}>Consultar rondas reemplazadas</button>
      {history.map(r => <details key={`${r.round}-${r.revision}`}><summary>Ronda {r.round} · versión {r.revision} anulada</summary>
        <p>Estos resultados y byes ya no suman puntos.</p>
        <p>Reemplazó {name(r.replacedByPlayerId)} · {new Date(r.replacedAt).toLocaleString()} · {r.replacementReason}</p>
        {r.matches.map(m => <article key={m.id}><h5>{name(m.playerOneId)} vs {name(m.playerTwoId)}</h5>
          {m.history.map(result => <p key={result.number}>Resultado {result.number}: {result.outcome === 'Draw' ? 'Empate' : `Ganó ${name(result.outcome === 'PlayerOneWin' ? m.playerOneId : m.playerTwoId)}`} · {result.kind === 'Forfeit' ? 'Forfeit' : 'Jugada'} · {result.reason} · {new Date(result.recordedAt).toLocaleString()}</p>)}
        </article>)}
        {r.byes.map(b => <p key={b.id}>Bye anulado: {name(b.playerId)}</p>)}
      </details>)}
    </fieldset>
    <p>Consultá las partidas para cargar resultados y la tabla para ver la clasificación actualizada.</p>
  </section>
}
