import React from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import Sidebar from './components/Sidebar';
import S1_ReviewRecovery from './pages/S1_ReviewRecovery';
import './index.css';

function App() {
  return (
    <Router>
      <div className="flex min-h-screen bg-gray-50 text-t-primary">
        <Sidebar />
        <main className="flex-1 overflow-x-hidden p-6 lg:p-10">
          <Routes>
            <Route path="/" element={<Navigate to="/scenarios/s1" />} />
            <Route path="/scenarios/s1" element={<S1_ReviewRecovery />} />
            {/* Future routes */}
          </Routes>
        </main>
      </div>
    </Router>
  );
}

export default App;
