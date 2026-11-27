import { useEffect, useRef, useState } from 'react'
import { AuthenticationError } from '../identity/api'
import { tournamentRequest, TournamentPermissionError, type Tournament } from './api'

type Event = { number: number; kind: string; reason: string; actorPlayerId: string; recordedAt: string; resultNumbers: number[] }
export function TournamentLifecycle({ tournament, onSaved, onAccessLost }: {
  tournament: Tournament; onSaved: (value: Tournament) => void; onAccessLost: () => void
}) {
  const [review, setReview] = useState<Tournament | null>(null)
  const [reason, setReason] = useState('')
  const [history, setHistory] = useState<Event[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const mounted = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  async function run(action: () => Promise<void>) {
    setBusy(true); setError('')
    try { await action() } catch (cause) {
      if (mounted.current) {
        if (cause instanceof AuthenticationError || cause instanceof TournamentPermissionError) onAccessLost()
        else setError(cause instanceof Error ? cause.message : 'No se pudo completar la operación.')
      }
    } finally { if (mounted.current) setBusy(false) }
  }
  if (tournament.status === 'Draft') return null
  return <section aria-label="Cierre y reapertura">
    <h4>{tournament.status === 'Finished' ? 'Torneo finalizado' : tournament.status === 'Correction' ? 'Reabierto para correcciones' : 'Cierre del torneo'}</h4>
    {tournament.finishedAt && <p>Último cierre: {new Date(tournament.finishedAt).toLocaleString()}</p>}
    <p>Finalizar conserva la clasificación y el historial; no publica el torneo.</p>
    {error && <p role="alert">{error}</p>}
    <fieldset disabled={busy}>
      {tournament.status === 'Finished' ? <form onSubmit={e => { e.preventDefault(); void run(async () => {
        const result = await tournamentRequest<Tournament>(`/${tournament.id}/reopen`, { expectedLifecycleVersion: tournament.lifecycleVersion, reason })
        if (mounted.current) onSaved(result)
      }) }}>
        <p>Reabrir permite corregir resultados y volver a finalizar. No permite generar ni reemplazar rondas.</p>
        <label>Motivo de reapertura<input required maxLength={500} value={reason} onChange={e => setReason(e.target.value)} /></label>
        <button disabled={!reason.trim()}>Reabrir para corregir</button>
      </form> : <>
        {tournament.status === 'Correction' && <p>Corregí los resultados necesarios y volvé a finalizar. Los cruces permanecen fijos.</p>}
        <button onClick={() => void run(async () => {
          const latest = await tournamentRequest<Tournament>(`/${tournament.id}`)
          if (mounted.current) { setReview(latest); onSaved(latest) }
        })}>Revisar cierre</button>
        {review && <>
          <p>Confirmá que querés finalizar. Se exige al menos una ronda y todos los jugadores asignados con todos los resultados cargados.</p>
          <button onClick={() => void run(async () => {
            const result = await tournamentRequest<Tournament>(`/${tournament.id}/finish`, {
              expectedLifecycleVersion: review.lifecycleVersion, expectedMatchesVersion: review.matchesVersion, expectedResultsVersion: review.resultsVersion,
            })
            if (mounted.current) { setReview(null); onSaved(result) }
          })}>Confirmar finalización</button>
          <button onClick={() => setReview(null)}>Cancelar cierre</button>
        </>}
      </>}
      <button onClick={() => void run(async () => {
        const events = await tournamentRequest<Event[]>(`/${tournament.id}/lifecycle`)
        if (mounted.current) setHistory(events)
      })}>Consultar historial de cierres</button>
    </fieldset>
    {history.map(e => <details key={e.number}><summary>{e.kind === 'Finished' ? 'Cierre' : 'Reapertura'} {e.number} · {e.reason}</summary>
      <p>Registró {e.actorPlayerId} · {new Date(e.recordedAt).toLocaleString()}</p>
      {e.kind === 'Finished' && <p>{e.resultNumbers.length} resultados preservados por su versión al cierre.</p>}
    </details>)}
  </section>
}
