import { useEffect, useRef, useState } from 'react'
import { AuthenticationError } from '../identity/api'
import { tournamentRequest, TournamentPermissionError, type Tournament } from './api'

export function TournamentPublication({ tournament, onSaved, onAccessLost }: {
  tournament: Tournament; onSaved: (value: Tournament) => void; onAccessLost: () => void
}) {
  const [confirm, setConfirm] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const mounted = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  async function save() {
    setBusy(true); setError('')
    try {
      const value = await tournamentRequest<Tournament>(`/${tournament.id}/publication`, {
        published: !tournament.publishedAt, expectedPublicationVersion: tournament.publicationVersion,
        expectedLifecycleVersion: tournament.lifecycleVersion,
      })
      if (mounted.current) { onSaved(value); setConfirm(false) }
    } catch (cause) {
      if (!mounted.current) return
      if (cause instanceof AuthenticationError || cause instanceof TournamentPermissionError) onAccessLost()
      else setError(cause instanceof Error ? cause.message : 'No se pudo cambiar la publicación.')
    } finally { if (mounted.current) setBusy(false) }
  }
  return <section aria-label="Publicación del torneo">
    <h4>Publicación</h4>
    {error && <p role="alert">{error}</p>}
    <p>Publicar permite consultar sin sesión nombres visibles, clasificación, cruces y resultados. Cada jugador decide si comparte su presentación.</p>
    {tournament.publishedAt ? <>
      <a href={`/#torneo=${tournament.id}`}>Ver página pública</a>
      <label>Enlace público<input readOnly value={`${window.location.origin}/#torneo=${tournament.id}`} /></label>
      <button disabled={busy} onClick={() => void save()}>Retirar publicación</button>
    </> : tournament.status === 'Finished' ? <>
      <label><input type="checkbox" checked={confirm} disabled={busy} onChange={e => setConfirm(e.target.checked)} />Confirmo publicar los resultados del torneo</label>
      <button disabled={!confirm || busy} onClick={() => void save()}>Publicar torneo</button>
    </> : <p>Solo se puede publicar después de finalizar. Reabrir retira la publicación y exige publicarlo nuevamente después del cierre.</p>}
  </section>
}
