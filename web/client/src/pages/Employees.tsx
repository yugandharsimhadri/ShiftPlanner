import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { deleteEmployee, getEmployees, getTracks } from '../api/endpoints'
import EmployeeFormModal from '../components/EmployeeFormModal'
import { useTeam } from '../team/TeamContext'
import type { Employee } from '../types'
import './Employees.css'

export default function Employees() {
  const { currentRole } = useTeam()
  const canEdit = currentRole === 'Editor' || currentRole === 'Admin'
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<Employee | 'new' | null>(null)

  const { data: employees, isLoading } = useQuery({ queryKey: ['employees'], queryFn: getEmployees })
  const { data: tracks } = useQuery({ queryKey: ['tracks'], queryFn: getTracks })

  const deleteMutation = useMutation({
    mutationFn: deleteEmployee,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['employees'] })
      queryClient.invalidateQueries({ queryKey: ['roster'] })
    },
  })

  function handleDelete(emp: Employee) {
    if (window.confirm(`Remove ${emp.name} (${emp.code})? This also deletes their roster history.`)) {
      deleteMutation.mutate(emp.id)
    }
  }

  return (
    <div className="employees-page">
      <div className="toolbar">
        <h2>Employees</h2>
        <div className="toolbar-spacer" />
        {canEdit && (
          <button className="btn" onClick={() => setEditing('new')} disabled={!tracks || tracks.length === 0}>
            Add employee
          </button>
        )}
      </div>

      {isLoading && <div className="empty-state">Loading employees…</div>}

      {employees && (
        <div className="card">
          <table className="data-table">
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Phone</th>
                <th>Track</th>
                <th>Role</th>
                <th>Type</th>
                <th>Status</th>
                {canEdit && <th></th>}
              </tr>
            </thead>
            <tbody>
              {employees.map((emp) => (
                <tr key={emp.id}>
                  <td className="mono">{emp.code}</td>
                  <td>{emp.name}</td>
                  <td className="mono">{emp.phone}</td>
                  <td>
                    {emp.track && (
                      <span className="pill" style={{ background: 'var(--accent-soft)', color: 'var(--ink)' }}>
                        <span className="pill-dot" style={{ background: emp.track.color }} />
                        {emp.track.name}
                        {emp.subtrack ? ` / ${emp.subtrack.name}` : ''}
                      </span>
                    )}
                  </td>
                  <td>{emp.role}</td>
                  <td>{emp.employmentType === 'FullTime' ? 'Full-time' : 'Part-time'}</td>
                  <td>
                    <span className={`badge${emp.status === 'Active' ? ' badge-active' : ''}`}>{emp.status}</span>
                  </td>
                  {canEdit && (
                    <td className="row-actions">
                      <button className="btn-secondary" onClick={() => setEditing(emp)}>
                        Edit
                      </button>
                      <button className="btn-danger" onClick={() => handleDelete(emp)}>
                        Remove
                      </button>
                    </td>
                  )}
                </tr>
              ))}
              {employees.length === 0 && (
                <tr>
                  <td colSpan={canEdit ? 8 : 7} className="empty-state">
                    No employees yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {canEdit && editing && tracks && (
        <EmployeeFormModal
          employee={editing === 'new' ? null : editing}
          tracks={tracks}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  )
}
