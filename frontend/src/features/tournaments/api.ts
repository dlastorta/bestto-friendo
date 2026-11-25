import { AuthenticationError, csrfHeaders } from '../identity/api'

export type Tournament = { id: string; name: string; gameId: string; status: 'Draft' }
export class TournamentPermissionError extends Error {}

export async function tournamentRequest<T>(path = '', body?: { name: string; gameId: string }): Promise<T> {
  const response = await fetch(`/api/tournaments${path}`, {
    method: body ? 'POST' : 'GET', credentials: 'same-origin', cache: 'no-store',
    headers: body ? await csrfHeaders() : { Accept: 'application/json' },
    ...(body ? { body: JSON.stringify(body) } : {}),
  })
  if (response.status === 401) throw new AuthenticationError()
  if (response.status === 403) throw new TournamentPermissionError('Necesitás el rol de organizador.')
  if (!response.ok) {
    const error = await response.json().catch(() => ({})) as { message?: string; errors?: Record<string, string[]> }
    throw new Error(error.message ?? Object.values(error.errors ?? {}).flat()[0] ?? 'No se pudo completar la operación.')
  }
  return response.json() as Promise<T>
}
