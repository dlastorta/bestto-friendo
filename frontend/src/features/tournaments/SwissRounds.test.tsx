import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { SwissRounds } from './SwissRounds'

afterEach(() => vi.unstubAllGlobals())
const response = (body: unknown, status = 200) => ({ ok: status < 400, status, json: async () => body })
const players = [{ playerId: 'a', displayName: 'Ana' }, { playerId: 'b', displayName: 'Bruno' }, { playerId: 'c', displayName: 'Ceci' }]
const round = { number: 1, actorPlayerId: 'a', generatedAt: '2026-11-27T10:00:00Z', participantOrder: ['c', 'a', 'b'], matches: [{ matchId: 'm', playerOneId: 'a', playerTwoId: 'b' }], byePlayerId: 'c' }

it('confirma una ronda completa, envía CSRF y muestra cruces, bye y sorteo auditado', async () => {
  let generated = false
  const request = vi.fn(async (url: string, init?: RequestInit) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url.endsWith('/participants')) return response(players)
    if (init?.method === 'POST') { generated = true; return response(round) }
    return response({ nextRound: generated ? 2 : 1, enabled: true, rounds: generated ? [round] : [] })
  })
  vi.stubGlobal('fetch', request)
  render(<SwissRounds tournamentId="cup" onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar rondas' }))
  expect(await screen.findByRole('button', { name: 'Generar ronda 1' })).toBeDisabled()
  fireEvent.click(screen.getByRole('checkbox', { name: /Confirmo generar/ }))
  fireEvent.click(screen.getByRole('button', { name: 'Generar ronda 1' }))
  expect(await screen.findByText('Ana vs Bruno')).toBeInTheDocument()
  expect(screen.getByText('Bye: Ceci')).toBeInTheDocument()
  expect(screen.getByText('Orden del sorteo')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Generar ronda 2' })).toBeDisabled()
  expect(request).toHaveBeenCalledWith('/api/tournaments/cup/rounds', expect.objectContaining({ body: JSON.stringify({ expectedRound: 1 }), headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }) }))
})

it('muestra bloqueo por resultados pendientes sin inventar una ronda', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url.endsWith('/participants')) return response(players)
    return init?.method === 'POST' ? response({ message: 'Faltan resultados de la ronda anterior.' }, 409) : response({ nextRound: 2, enabled: true, rounds: [round] })
  }))
  render(<SwissRounds tournamentId="cup" onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar rondas' }))
  await screen.findByRole('checkbox', { name: /Confirmo generar/ })
  fireEvent.click(screen.getByRole('checkbox', { name: /Confirmo generar/ }))
  fireEvent.click(screen.getByRole('button', { name: 'Generar ronda 2' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Faltan resultados')
  expect(screen.queryByRole('article', { name: 'Ronda generada 2' })).not.toBeInTheDocument()
})

it('conserva la modalidad manual de un torneo histórico', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.endsWith('/participants') ? players : { nextRound: 1, enabled: false, rounds: [] })))
  render(<SwissRounds tournamentId="cup" onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar rondas' }))
  expect(await screen.findByText(/modalidad manual histórica/)).toBeInTheDocument()
  expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
})

it.each([401, 403])('retira acceso privado cuando la sesión pierde permisos (%s)', async status => {
  const lost = vi.fn()
  vi.stubGlobal('fetch', vi.fn(async () => response({}, status)))
  render(<SwissRounds tournamentId="cup" onAccessLost={lost} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar rondas' }))
  await waitFor(() => expect(lost).toHaveBeenCalledOnce())
})

it('regenera únicamente la última ronda con motivo, confirmación y versiones consultadas', async () => {
  const request = vi.fn(async (url: string, init?: RequestInit) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url.endsWith('/participants')) return response(players)
    if (init?.method === 'POST') return response({ ...round, revision: 2 })
    return response({ nextRound: 2, enabled: true, rounds: [round], matchesVersion: 2, resultsVersion: 1, lifecycleVersion: 0 })
  })
  vi.stubGlobal('fetch', request)
  render(<SwissRounds tournamentId="cup" onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar rondas' }))
  fireEvent.click(await screen.findByRole('checkbox', { name: 'Reemplazar únicamente la última ronda' }))
  expect(screen.getByRole('button', { name: 'Generar ronda 1 nuevamente' })).toBeDisabled()
  fireEvent.change(screen.getByLabelText('Motivo de regeneración'), { target: { value: 'Resultado corregido' } })
  fireEvent.click(screen.getByRole('checkbox', { name: /Confirmo generar/ }))
  fireEvent.click(screen.getByRole('button', { name: 'Generar ronda 1 nuevamente' }))
  await waitFor(() => expect(request).toHaveBeenCalledWith('/api/tournaments/cup/rounds/regenerate', expect.objectContaining({
    body: JSON.stringify({ round: 1, reason: 'Resultado corregido', expectedMatchesVersion: 2, expectedResultsVersion: 1, expectedLifecycleVersion: 0 }),
    headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
  })))
})
