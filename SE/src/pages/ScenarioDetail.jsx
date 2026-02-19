import React from 'react';
import { useParams, Navigate } from 'react-router-dom';
import ScenarioPage from '../components/ScenarioPage';
import { scenarioById } from '../data';

const ScenarioDetail = () => {
    const { id } = useParams();

    const data = scenarioById[id];
    if (data) {
        return <ScenarioPage data={data} />;
    }

    return <Navigate to="/" replace />;
};

export default ScenarioDetail;
