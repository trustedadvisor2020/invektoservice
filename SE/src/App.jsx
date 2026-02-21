import React from 'react';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import Sidebar from './components/Sidebar';
import Landing from './pages/Landing';
import ScenarioDetail from './pages/ScenarioDetail';
import Capabilities from './pages/Capabilities';
import HealthCheck from './pages/HealthCheck';
import './index.css';

function App() {
    return (
        <Router>
            <div className="flex min-h-screen bg-gray-50 text-t-primary">
                <Sidebar />
                <main className="flex-1 overflow-x-hidden p-6 lg:p-10">
                    <Routes>
                        <Route path="/" element={<Landing />} />
                        <Route path="/scenarios/:id" element={<ScenarioDetail />} />
                        <Route path="/capabilities" element={<Capabilities />} />
                        <Route path="/health-check" element={<HealthCheck />} />
                    </Routes>
                </main>
            </div>
        </Router>
    );
}

export default App;
