import { useEffect, useRef } from 'react'
import type { ShiftType } from '../types'
import './ShiftPopup.css'

interface Props {
  x: number
  y: number
  shiftTypes: ShiftType[]
  currentCode: string | null
  onSelect: (code: string | null) => void
  onClose: () => void
}

export default function ShiftPopup({ x, y, shiftTypes, currentCode, onSelect, onClose }: Props) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose()
    }
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('mousedown', handleClick)
    document.addEventListener('keydown', handleKey)
    return () => {
      document.removeEventListener('mousedown', handleClick)
      document.removeEventListener('keydown', handleKey)
    }
  }, [onClose])

  const clampedX = Math.min(x, window.innerWidth - 220)
  const clampedY = Math.min(y, window.innerHeight - 260)

  return (
    <div className="shift-popup" ref={ref} style={{ left: clampedX, top: clampedY }}>
      <div className="shift-popup-title">Assign shift</div>
      {shiftTypes.map((st) => (
        <button
          key={st.code}
          className={`shift-popup-option${currentCode === st.code ? ' selected' : ''}`}
          style={{ '--chip-color': st.color } as React.CSSProperties}
          onClick={() => onSelect(st.code)}
        >
          <span className="shift-popup-code">{st.code}</span>
          <span className="shift-popup-name">{st.name}</span>
          {st.start && st.end && (
            <span className="shift-popup-time mono">
              {st.start.slice(0, 5)}–{st.end.slice(0, 5)}
            </span>
          )}
        </button>
      ))}
      <button className="shift-popup-option shift-popup-clear" onClick={() => onSelect(null)}>
        <span className="shift-popup-code">—</span>
        <span className="shift-popup-name">Clear</span>
      </button>
    </div>
  )
}
