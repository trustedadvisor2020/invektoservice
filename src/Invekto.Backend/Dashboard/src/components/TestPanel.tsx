import { useState } from 'react';
import { Play, CheckCircle, XCircle, Loader2, AlertTriangle } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent } from './ui/Card';
import { Button } from './ui/Button';
import { Badge } from './ui/Badge';
import { api } from '../lib/api';

interface TestResult {
  name: string;
  status: 'pending' | 'running' | 'passed' | 'failed' | 'warning';
  message?: string;
  duration?: number;
}

interface ServiceTests {
  serviceName: string;
  tests: TestResult[];
  isRunning: boolean;
}

const serviceTestConfigs: Record<string, { name: string; tests: { name: string; endpoint: string; method: 'GET' | 'POST'; expectedStatus?: number; useProxy?: boolean }[] }> = {
  'Invekto.Backend': {
    name: 'Backend',
    tests: [
      { name: 'Health Check', endpoint: '/health', method: 'GET', expectedStatus: 200 },
      { name: 'Ops Auth Check', endpoint: '/api/ops/health', method: 'GET', expectedStatus: 200 },
      { name: 'Log Stream', endpoint: '/api/ops/logs/stream?limit=1', method: 'GET', expectedStatus: 200 },
      { name: 'Error Stats', endpoint: '/api/ops/stats/errors?hours=1', method: 'GET', expectedStatus: 200 },
    ],
  },
  'Invekto.ChatAnalysis': {
    name: 'ChatAnalysis',
    tests: [
      { name: 'Health Check', endpoint: '/api/ops/test/chatanalysis/health', method: 'GET', expectedStatus: 200, useProxy: true },
      { name: 'Ready Check', endpoint: '/api/ops/test/chatanalysis/ready', method: 'GET', expectedStatus: 200, useProxy: true },
    ],
  },
  'Invekto.Automation': {
    name: 'Automation',
    tests: [
      { name: 'Health Check', endpoint: '/api/ops/test/automation/health', method: 'GET', expectedStatus: 200, useProxy: true },
      { name: 'Ready Check', endpoint: '/api/ops/test/automation/ready', method: 'GET', expectedStatus: 200, useProxy: true },
    ],
  },
};

export function TestPanel() {
  const [serviceTests, setServiceTests] = useState<Record<string, ServiceTests>>(() => {
    const initial: Record<string, ServiceTests> = {};
    Object.entries(serviceTestConfigs).forEach(([key, config]) => {
      initial[key] = {
        serviceName: config.name,
        tests: config.tests.map(t => ({ name: t.name, status: 'pending' })),
        isRunning: false,
      };
    });
    return initial;
  });

  const [isRunningAll, setIsRunningAll] = useState(false);

  const runTest = async (
    serviceName: string,
    testIndex: number,
    endpoint: string,
    method: 'GET' | 'POST',
    expectedStatus: number = 200,
    useProxy: boolean = false
  ): Promise<TestResult> => {
    const startTime = Date.now();
    const testName = serviceTests[serviceName].tests[testIndex].name;

    try {
      const response = await fetch(`${api.baseUrl}${endpoint}`, {
        method,
        headers: api.getAuthHeaders(),
      });

      const duration = Date.now() - startTime;

      // For proxy endpoints, check the response body for success
      if (useProxy && response.ok) {
        const data = await response.json();
        if (data.success) {
          return {
            name: testName,
            status: 'passed',
            message: `${data.statusCode} OK`,
            duration: data.durationMs || duration,
          };
        } else {
          return {
            name: testName,
            status: 'failed',
            message: data.message || 'Failed',
            duration: data.durationMs || duration,
          };
        }
      }

      // Direct endpoint check
      if (response.status === expectedStatus) {
        return {
          name: testName,
          status: 'passed',
          message: `${response.status} OK`,
          duration,
        };
      } else if (response.status === 401) {
        return {
          name: testName,
          status: 'warning',
          message: 'Auth required (401)',
          duration,
        };
      } else {
        return {
          name: testName,
          status: 'failed',
          message: `Expected ${expectedStatus}, got ${response.status}`,
          duration,
        };
      }
    } catch (error) {
      const duration = Date.now() - startTime;
      return {
        name: testName,
        status: 'failed',
        message: error instanceof Error ? error.message : 'Connection failed',
        duration,
      };
    }
  };

  const runServiceTests = async (serviceName: string) => {
    const config = serviceTestConfigs[serviceName];
    if (!config) return;

    setServiceTests(prev => ({
      ...prev,
      [serviceName]: {
        ...prev[serviceName],
        isRunning: true,
        tests: prev[serviceName].tests.map(t => ({ ...t, status: 'pending' as const })),
      },
    }));

    for (let i = 0; i < config.tests.length; i++) {
      const test = config.tests[i];

      // Set current test to running
      setServiceTests(prev => ({
        ...prev,
        [serviceName]: {
          ...prev[serviceName],
          tests: prev[serviceName].tests.map((t, idx) =>
            idx === i ? { ...t, status: 'running' as const } : t
          ),
        },
      }));

      // Run the test
      const result = await runTest(serviceName, i, test.endpoint, test.method, test.expectedStatus, test.useProxy);

      // Update with result
      setServiceTests(prev => ({
        ...prev,
        [serviceName]: {
          ...prev[serviceName],
          tests: prev[serviceName].tests.map((t, idx) =>
            idx === i ? result : t
          ),
        },
      }));
    }

    setServiceTests(prev => ({
      ...prev,
      [serviceName]: {
        ...prev[serviceName],
        isRunning: false,
      },
    }));
  };

  const runAllTests = async () => {
    setIsRunningAll(true);
    for (const serviceName of Object.keys(serviceTestConfigs)) {
      await runServiceTests(serviceName);
    }
    setIsRunningAll(false);
  };

  const getStatusIcon = (status: TestResult['status']) => {
    switch (status) {
      case 'passed':
        return <CheckCircle className="w-4 h-4 text-emerald-500" />;
      case 'failed':
        return <XCircle className="w-4 h-4 text-red-500" />;
      case 'warning':
        return <AlertTriangle className="w-4 h-4 text-amber-500" />;
      case 'running':
        return <Loader2 className="w-4 h-4 text-blue-500 animate-spin" />;
      default:
        return <div className="w-4 h-4 rounded-full bg-slate-200" />;
    }
  };

  const getStatusVariant = (status: TestResult['status']): 'success' | 'error' | 'warning' | 'default' => {
    switch (status) {
      case 'passed':
        return 'success';
      case 'failed':
        return 'error';
      case 'warning':
        return 'warning';
      default:
        return 'default';
    }
  };

  const getTestSummary = (tests: TestResult[]): { passed: number; failed: number; warning: number } => {
    return tests.reduce(
      (acc, t) => {
        if (t.status === 'passed') acc.passed++;
        if (t.status === 'failed') acc.failed++;
        if (t.status === 'warning') acc.warning++;
        return acc;
      },
      { passed: 0, failed: 0, warning: 0 }
    );
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle>Quick Tests</CardTitle>
          <Button
            size="sm"
            onClick={runAllTests}
            disabled={isRunningAll || Object.values(serviceTests).some(s => s.isRunning)}
          >
            {isRunningAll ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <Play className="w-4 h-4" />
            )}
            <span>Run All</span>
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {Object.entries(serviceTests).map(([serviceName, service]) => {
          const summary = getTestSummary(service.tests);
          return (
            <div key={serviceName} className="p-4 bg-slate-50 rounded-lg">
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                  <span className="font-medium text-slate-900">{service.serviceName}</span>
                  {summary.passed + summary.failed + summary.warning > 0 && (
                    <div className="flex gap-1">
                      {summary.passed > 0 && (
                        <Badge variant="success">{summary.passed} passed</Badge>
                      )}
                      {summary.failed > 0 && (
                        <Badge variant="error">{summary.failed} failed</Badge>
                      )}
                      {summary.warning > 0 && (
                        <Badge variant="warning">{summary.warning} warning</Badge>
                      )}
                    </div>
                  )}
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => runServiceTests(serviceName)}
                  disabled={service.isRunning || isRunningAll}
                >
                  {service.isRunning ? (
                    <Loader2 className="w-3.5 h-3.5 animate-spin" />
                  ) : (
                    <Play className="w-3.5 h-3.5" />
                  )}
                  <span>Run</span>
                </Button>
              </div>

              <div className="space-y-2">
                {service.tests.map((test, idx) => (
                  <div
                    key={idx}
                    className="flex items-center justify-between p-2 bg-white rounded-lg border border-slate-100"
                  >
                    <div className="flex items-center gap-2">
                      {getStatusIcon(test.status)}
                      <span className="text-sm text-slate-700">{test.name}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      {test.duration !== undefined && (
                        <span className="text-xs text-slate-400">{test.duration}ms</span>
                      )}
                      {test.message && test.status !== 'pending' && test.status !== 'running' && (
                        <Badge variant={getStatusVariant(test.status)}>
                          {test.message}
                        </Badge>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
