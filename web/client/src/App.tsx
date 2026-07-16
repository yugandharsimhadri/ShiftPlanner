import { Routes, Route } from 'react-router-dom'
import './styles/ui.css'
import Layout from './components/Layout'
import Login from './pages/Login'
import CreateTeam from './pages/CreateTeam'
import SelectTeam from './pages/SelectTeam'
import Roster from './pages/Roster'
import TeamMembers from './pages/TeamMembers'
import Settings from './pages/Settings'
import Reports from './pages/Reports'
import Live from './pages/Live'
import ManagerDashboard from './pages/ManagerDashboard'
import Profile from './pages/Profile'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/create-team" element={<CreateTeam />} />
      <Route path="/select-team" element={<SelectTeam />} />
      <Route element={<Layout />}>
        <Route path="/" element={<Roster />} />
        <Route path="/live" element={<Live />} />
        <Route path="/manager" element={<ManagerDashboard />} />
        <Route path="/members" element={<TeamMembers />} />
        <Route path="/settings" element={<Settings />} />
        <Route path="/reports" element={<Reports />} />
        <Route path="/profile" element={<Profile />} />
      </Route>
    </Routes>
  )
}
