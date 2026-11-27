import { useEffect, useRef, useState } from 'react'
import { AuthenticationError } from '../identity/api'
import { tournamentRequest, TournamentPermissionError, type Tournament } from './api'

export function TournamentStart({ tournament, onSaved, onAccessLost }: {
  tournament: Tournament; onSaved: (value: Tournament) => void; onAccessLost: () => void
}) {
  const [confirming, setConfirming] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const mounted = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  async function start() {
    setBusy(true); setError('')
    try {
      const result = await tournamentRequest<Tournament>(`/${tournament.id}/start`, {
        expectedRulesVersion: tournament.rulesVersion, expectedParticipationVersion: tournament.participationVersion,
      })
      if (mounted.current) onSaved(result)
    } catch (cause) {
      if (mounted.current) {
        if (cause instanceof AuthenticationError || cause instanceof TournamentPermissionError) onAccessLost()
        else setError(cause instanceof Error ? cause.message : 'No se pudo iniciar el torneo.')
      }
    } finally { if (mounted.current) setBusy(false) }
  }
  if (tournament.status !== 'Draft') return <p role="status">{tournament.status === 'Finished' ? 'Torneo finalizado.' : tournament.status === 'Correction' ? 'Torneo reabierto para correcciones.' : 'Torneo en curso.'} Reglas históricas y presentaciones bloqueadas. Inicio: {tournament.startedAt ? new Date(tournament.startedAt).toLocaleString() : 'registrado'}.</p>
  return <section aria-label="Inicio del torneo">
    {error && <p role="alert">{error}</p>}
    {!confirming ? <button onClick={() => setConfirming(true)}>Comenzar torneo</button> : <>
      <p>Se requieren reglas configuradas, al menos dos jugadores y todos los mazos presentados. Al iniciar se guardan las reglas históricas y se bloquean inscripciones y cambios. No se puede volver a borrador.</p>
      <button disabled={busy} onClick={() => void start()}>Confirmar inicio</button>
      <button disabled={busy} onClick={() => setConfirming(false)}>Cancelar inicio</button>
    </>}
  </section>
}
