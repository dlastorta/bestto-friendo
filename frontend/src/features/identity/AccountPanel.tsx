import { useEffect, useState, type FormEvent } from 'react'
import { AuthenticationError, currentProfile, identityRequest, type PlayerProfile } from './api'

export function AccountPanel({ onChange }: { onChange: (profile: PlayerProfile | null) => void }) {
  const [profile, setProfile] = useState<PlayerProfile | null>(null)
  const [registering, setRegistering] = useState(false)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [busy, setBusy] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    void currentProfile().then(value => {
      if (!active) return
      setProfile(value); setDisplayName(value?.displayName ?? ''); onChange(value)
    }).catch(reason => { if (active) setError((reason as Error).message) })
      .finally(() => { if (active) setBusy(false) })
    return () => { active = false }
  }, [onChange])

  const clearSession = () => {
    setProfile(null); onChange(null); setPassword(''); setDisplayName(''); setRegistering(false)
  }

  const handleError = (reason: unknown) => {
    if (profile && reason instanceof AuthenticationError) clearSession()
    setError(reason instanceof Error ? reason.message : 'No se pudo completar la operación.')
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault(); setBusy(true); setError(null); setMessage(null)
    try {
      if (profile) {
        const updated = await identityRequest<PlayerProfile>('/api/me', 'PUT', { displayName })
        setProfile(updated); onChange(updated); setMessage('Perfil actualizado.')
      } else {
        if (registering) await identityRequest('/api/auth/register', 'POST', { email, password, displayName })
        await identityRequest('/api/auth/login', 'POST', { email, password })
        const updated = await currentProfile()
        setProfile(updated); setDisplayName(updated?.displayName ?? ''); onChange(updated); setPassword('')
      }
    } catch (reason) { handleError(reason) }
    finally { setBusy(false) }
  }

  const logout = async () => {
    setBusy(true); setError(null); setMessage(null)
    try {
      await identityRequest('/api/auth/logout', 'POST', {})
      clearSession()
    } catch (reason) { handleError(reason) }
    finally { setBusy(false) }
  }

  return <section className="account-section" aria-labelledby="account-heading">
    <h2 id="account-heading">{profile ? 'Mi perfil' : 'Tu cuenta de jugador'}</h2>
    <p>{profile ? (profile.canManageCatalog ? 'Administrador del catálogo' : 'Jugador') : 'Ingresá para acceder a tu perfil personal.'}</p>
    {profile?.canOrganizeTournaments && <p>Organizador de torneos</p>}
    <form className="game-form" onSubmit={event => void submit(event)}>
      {(registering || profile) && <>
        <label htmlFor="player-name">Nombre del jugador</label>
        <input id="player-name" value={displayName} onChange={event => setDisplayName(event.target.value)} required />
      </>}
      {!profile && <>
        <label htmlFor="account-email">Email</label>
        <input id="account-email" type="email" autoComplete="username" value={email} onChange={event => setEmail(event.target.value)} required />
        <label htmlFor="account-password">Contraseña</label>
        <input id="account-password" type="password" autoComplete={registering ? 'new-password' : 'current-password'} value={password} onChange={event => setPassword(event.target.value)} required />
        {registering && <p className="input-help">Usá una contraseña de entre 12 y 128 caracteres.</p>}
      </>}
      {error && <p role="alert" className="form-alert form-alert--error">{error}</p>}
      {message && <p role="status">{message}</p>}
      <button type="submit" disabled={busy}>{busy ? 'Procesando…' : profile ? 'Guardar perfil' : registering ? 'Crear cuenta' : 'Iniciar sesión'}</button>
      {profile ? <button type="button" disabled={busy} onClick={() => void logout()}>Cerrar sesión</button>
        : <button type="button" disabled={busy} onClick={() => { setRegistering(!registering); setError(null); setPassword('') }}>
          {registering ? 'Ya tengo cuenta' : 'Registrarme'}
        </button>}
    </form>
  </section>
}
