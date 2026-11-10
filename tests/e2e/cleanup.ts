import type { FullConfig } from '@playwright/test'
import { readdir, rmdir, unlink } from 'node:fs/promises'
import path from 'node:path'

export default async function cleanup(config: FullConfig) {
  const database = config.metadata.database
  if (typeof database !== 'string') throw new Error('Test database metadata is missing.')
  for (const suffix of ['', '-wal', '-shm']) {
    try { await unlink(database + suffix) }
    catch (error) {
      if (!(error instanceof Error && 'code' in error && error.code === 'ENOENT')) throw error
    }
  }
  const models = config.metadata.models
  if (typeof models !== 'string') throw new Error('Test model directory metadata is missing.')
  for (const file of await readdir(models)) await unlink(path.join(models, file))
  await rmdir(models)
}
