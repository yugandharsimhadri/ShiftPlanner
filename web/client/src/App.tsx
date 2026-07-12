import { Routes, Route } from 'react-router-dom'
import './styles/ui.css'
import Layout from './components/Layout'
import Login from './pages/Login'
import CreateTeam from './pages/CreateTeam'
import SelectTeam from './pages/SelectTeam'
import Roster from './pages/Roster'
import Employees from './pages/Employees'
import Members from './pages/Members'
import Settings from './pages/Settings'
import Reports from './pages/Reports'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/create-team" element={<CreateTeam />} />
      <Route path="/select-team" element={<SelectTeam />} />
      <Route element={<Layout />}>
        <Route path="/" element={<Roster />} />
        <Route path="/employees" element={<Employees />} />
        <Route path="/members" element={<Members />} />
        <Route path="/settings" element={<Settings />} />
        <Route path="/reports" element={<Reports />} />
      </Route>
    </Routes>
  )
}
