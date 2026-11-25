export type PlayerProfile = { id: string; displayName: string; canManageCatalog: boolean; canOrganizeTournaments: boolean }

export class AuthenticationError extends Error {
  constructor() {
    super('Email o contraseña incorrectos, cuenta bloqueada o sesión vencida.')
    this.name = 'AuthenticationError'
  }
}

export async function csrfHeaders(): Promise<Record<string, string>> {
  const response = await fetch('/api/auth/csrf', { credentials: 'same-origin' })
  if (!response.ok) throw new Error('No se pudo preparar el formulario. Intentá nuevamente.')
  const { token } = await response.json() as { token: string }
  return { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token }
}

export async function identityRequest<T>(url: string, method = 'GET', body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method, credentials: 'same-origin',
    headers: method === 'GET' ? { Accept: 'application/json' } : await csrfHeaders(),
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  })
  if (response.status === 401) throw new AuthenticationError()
  if (!response.ok) {
    const error = await response.json().catch(() => ({})) as { message?: string; title?: string; errors?: Record<string, string[]> }
    throw new Error(error.message ?? Object.values(error.errors ?? {}).flat()[0] ?? error.title ?? 'No se pudo completar la operación.')
  }
  return response.status === 204 || response.status === 201 ? undefined as T : response.json() as Promise<T>
}

export async function currentProfile(): Promise<PlayerProfile | null> {
  const response = await fetch('/api/me', { credentials: 'same-origin' })
  if (response.status === 401) return null
  if (!response.ok) throw new Error('No se pudo consultar tu sesión. Reintentá.')
  return response.json() as Promise<PlayerProfile>
}
