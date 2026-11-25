import { csrfHeaders } from '../identity/api'

export type GameFaction = {
  id: string
  name: string
}

export type Game = {
  id: string
  name: string
  minimumFactions: number
  maximumFactions: number
  factions: GameFaction[]
}

export type CreateGamePayload = {
  name: string
  factions: string[]
  minimumFactions: number
  maximumFactions: number
}

export class GameApiError extends Error {
  constructor(message: string, readonly status: number) {
    super(message)
    this.name = 'GameApiError'
  }
}

function getErrorMessage(payload: unknown): string | undefined {
  if (typeof payload !== 'object' || payload === null) return undefined
  if ('message' in payload && typeof payload.message === 'string') return payload.message
  if ('errors' in payload && typeof payload.errors === 'object' && payload.errors !== null) {
    const lists = Object.values(payload.errors).filter(Array.isArray) as string[][]
    return lists.flat()[0]
  }
  return undefined
}

async function fromApi<T>(response: Response): Promise<T> {
  const payload: unknown = await response.json().catch(() => null)
  if (!response.ok) {
    throw new GameApiError(getErrorMessage(payload) ?? `Error HTTP ${response.status}`, response.status)
  }
  return payload as T
}

export async function listGames(): Promise<Game[]> {
  return fromApi<Game[]>(await fetch('/api/games', {
    headers: { Accept: 'application/json' },
  }))
}

export async function createGame(payload: CreateGamePayload): Promise<Game> {
  return fromApi<Game>(await fetch('/api/games', {
    method: 'POST',
    headers: { ...await csrfHeaders(), Accept: 'application/json' },
    body: JSON.stringify(payload),
  }))
}
