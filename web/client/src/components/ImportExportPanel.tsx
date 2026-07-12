import { useRef, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { downloadExport, importEmployees } from '../api/endpoints'
import { monthLabel } from '../lib/dates'
import type { ImportResult } from '../types'

interface Props {
  year: number
  month: number
  canImport: boolean
  onClose: () => void
}

export default function ImportExportPanel({ year, month, canImport, onClose }: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [importResult, setImportResult] = useState<ImportResult | null>(null)
  const [exporting, setExporting] = useState<'excel' | 'csv' | null>(null)

  const importMutation = useMutation({
    mutationFn: (file: File) => importEmployees(file),
    onSuccess: (result) => setImportResult(result),
  })

  async function handleExport(kind: 'excel' | 'csv') {
    setExporting(kind)
    try {
      await downloadExport(kind, year, month)
    } finally {
      setExporting(null)
    }
  }

  function handleImport() {
    if (selectedFile) importMutation.mutate(selectedFile)
  }

  return (
    <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <h2>Import / Export</h2>

        <section style={{ marginBottom: 24 }}>
          <h3 style={{ fontSize: 13, textTransform: 'uppercase', letterSpacing: 0.4, color: 'var(--ink-soft)', marginBottom: 10 }}>
            Export roster — {monthLabel(year, month)}
          </h3>
          <div style={{ display: 'flex', gap: 10 }}>
            <button className="btn-secondary" onClick={() => handleExport('excel')} disabled={exporting !== null}>
              {exporting === 'excel' ? 'Exporting…' : 'Export .xlsx'}
            </button>
            <button className="btn-secondary" onClick={() => handleExport('csv')} disabled={exporting !== null}>
              {exporting === 'csv' ? 'Exporting…' : 'Export .csv'}
            </button>
          </div>
        </section>

        {canImport && (
          <section>
            <h3 style={{ fontSize: 13, textTransform: 'uppercase', letterSpacing: 0.4, color: 'var(--ink-soft)', marginBottom: 10 }}>
              Import employees (.csv or .xlsx)
            </h3>
            <p style={{ fontSize: 12, color: 'var(--ink-faint)', marginTop: 0 }}>
              Columns: Name, Phone, Email, Track, Subtrack, Role, EmploymentType, JoinDate, WeeklyOff, Status, Notes
            </p>
            <input
              ref={fileInputRef}
              type="file"
              accept=".csv,.xlsx"
              onChange={(e) => setSelectedFile(e.target.files?.[0] ?? null)}
            />
            <div className="modal-actions" style={{ marginTop: 14 }}>
              <button className="btn" onClick={handleImport} disabled={!selectedFile || importMutation.isPending}>
                {importMutation.isPending ? 'Importing…' : 'Import'}
              </button>
            </div>

            {importResult && (
              <div style={{ marginTop: 16 }}>
                <p>
                  Imported <strong>{importResult.imported}</strong> employee{importResult.imported === 1 ? '' : 's'}.
                </p>
                {importResult.errors.length > 0 && (
                  <>
                    <p style={{ fontWeight: 600, marginBottom: 6 }}>Rows with errors ({importResult.errors.length}):</p>
                    <div style={{ maxHeight: 180, overflowY: 'auto', border: '1px solid var(--line)', borderRadius: 6 }}>
                      <table className="data-table">
                        <thead>
                          <tr>
                            <th>Row</th>
                            <th>Message</th>
                          </tr>
                        </thead>
                        <tbody>
                          {importResult.errors.map((err, i) => (
                            <tr key={i}>
                              <td className="mono">{err.row}</td>
                              <td>{err.message}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </>
                )}
              </div>
            )}
          </section>
        )}

        <div className="modal-actions">
          <button className="btn-secondary" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  )
}
