import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { Standings } from './Standings'

afterEach(() => vi.unstubAllGlobals())
it('muestra puestos compartidos y recarga puntos desde el servidor', async () => {
  let points = 1.5
  vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, status: 200, json: async () => [
    { playerId: 'a', displayName: 'Ana', position: 1, points },
    { playerId: 'b', displayName: 'Beto', position: 1, points },
    { playerId: 'c', displayName: 'Ceci', position: 3, points: 0 },
  ] })))
  render(<Standings tournamentId="cup" onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar tabla' }))
  const table = await screen.findByRole('table')
  expect(within(table).getAllByRole('row')).toHaveLength(4)
  expect(within(table).getByRole('row', { name: '3 Ceci 0' })).toBeInTheDocument()
  points = 3
  fireEvent.click(screen.getByRole('button', { name: 'Consultar tabla' }))
  expect(await screen.findByRole('row', { name: '1 Ana 3' })).toBeInTheDocument()
})
it.each([401, 403])('retira tabla y notifica pérdida de acceso con %s', async status => {
  const lost = vi.fn()
  vi.stubGlobal('fetch', vi.fn(async () => ({ ok: false, status })))
  render(<Standings tournamentId="cup" onAccessLost={lost} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar tabla' }))
  await waitFor(() => expect(lost).toHaveBeenCalledOnce())
  expect(screen.queryByRole('table')).not.toBeInTheDocument()
})
