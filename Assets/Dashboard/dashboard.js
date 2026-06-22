/**
 * WinForge Telemetry Dashboard
 *
 * Copyright 2026 Julien Bombled
 * Licensed under the Apache License, Version 2.0
 */

let charts = {};

const chartColors = {
    primary: '#e94560',
    secondary: '#0ea5e9',
    success: '#22c55e',
    warning: '#f59e0b',
    error: '#ef4444',
    purple: '#8b5cf6',
    pink: '#ec4899',
    cyan: '#06b6d4',
    lime: '#84cc16',
    orange: '#f97316'
};

const colorPalette = [
    chartColors.primary,
    chartColors.secondary,
    chartColors.success,
    chartColors.purple,
    chartColors.warning,
    chartColors.pink,
    chartColors.cyan,
    chartColors.lime,
    chartColors.orange,
    chartColors.error
];

async function loadData() {
    const contentEl = document.getElementById('content');

    try {
        const response = await fetch('telemetry-data.json');
        if (!response.ok) {
            throw new Error('Failed to load telemetry data');
        }

        const data = await response.json();
        renderDashboard(data);

        document.getElementById('lastUpdated').textContent =
            `Last updated: ${new Date(data.generatedAt).toLocaleString()}`;

    } catch (error) {
        contentEl.innerHTML = `
            <div class="error-message">
                <p><strong>Error loading telemetry data</strong></p>
                <p>${error.message}</p>
                <p style="margin-top: 10px; font-size: 0.9em;">
                    Run <code>Export-TelemetryReport</code> in PowerShell to generate the data file.
                </p>
            </div>
        `;
    }
}

function renderDashboard(data) {
    const contentEl = document.getElementById('content');
    const summary = data.summary;

    contentEl.innerHTML = `
        <div class="stats-grid">
            <div class="stat-card">
                <div class="value">${summary.Deployments.Total}</div>
                <div class="label">Total Deployments</div>
            </div>
            <div class="stat-card success">
                <div class="value">${summary.Deployments.SuccessRate}</div>
                <div class="label">Success Rate</div>
            </div>
            <div class="stat-card">
                <div class="value">${summary.Applications.TotalInstalled}</div>
                <div class="label">Apps Installed</div>
            </div>
            <div class="stat-card warning">
                <div class="value">${summary.Performance.AverageDeploymentMinutes}m</div>
                <div class="label">Avg. Deployment Time</div>
            </div>
        </div>

        <div class="charts-grid">
            <div class="chart-card">
                <h3>Deployment Results</h3>
                <div class="chart-container">
                    <canvas id="deploymentChart"></canvas>
                </div>
            </div>

            <div class="chart-card">
                <h3>Installation Methods</h3>
                <div class="chart-container">
                    <canvas id="methodChart"></canvas>
                </div>
            </div>

            <div class="chart-card">
                <h3>Applications by Category</h3>
                <div class="chart-container">
                    <canvas id="categoryChart"></canvas>
                </div>
            </div>

            <div class="chart-card">
                <h3>Top Installed Applications</h3>
                <div class="chart-container">
                    <canvas id="topAppsChart"></canvas>
                </div>
            </div>
        </div>
    `;

    // Destroy existing charts
    Object.values(charts).forEach(chart => chart.destroy());
    charts = {};

    // Create charts
    createDeploymentChart(data.charts.deploymentPie);
    createMethodChart(data.charts.methodBar);
    createCategoryChart(data.charts.categoryBar);
    createTopAppsChart(data.charts.topAppsBar);
}

function createDeploymentChart(chartData) {
    const ctx = document.getElementById('deploymentChart').getContext('2d');

    charts.deployment = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: chartData.labels,
            datasets: [{
                data: chartData.data,
                backgroundColor: [
                    chartColors.success,
                    chartColors.error,
                    chartColors.warning
                ],
                borderWidth: 0
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        color: '#eaeaea',
                        padding: 15
                    }
                }
            }
        }
    });
}

function createMethodChart(chartData) {
    const ctx = document.getElementById('methodChart').getContext('2d');

    charts.method = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: chartData.labels,
            datasets: [{
                label: 'Installations',
                data: chartData.data,
                backgroundColor: colorPalette.slice(0, chartData.labels.length),
                borderWidth: 0,
                borderRadius: 4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            indexAxis: 'y',
            plugins: {
                legend: {
                    display: false
                }
            },
            scales: {
                x: {
                    ticks: { color: '#a0a0a0' },
                    grid: { color: 'rgba(255,255,255,0.1)' }
                },
                y: {
                    ticks: { color: '#eaeaea' },
                    grid: { display: false }
                }
            }
        }
    });
}

function createCategoryChart(chartData) {
    const ctx = document.getElementById('categoryChart').getContext('2d');

    charts.category = new Chart(ctx, {
        type: 'pie',
        data: {
            labels: chartData.labels,
            datasets: [{
                data: chartData.data,
                backgroundColor: colorPalette.slice(0, chartData.labels.length),
                borderWidth: 0
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'right',
                    labels: {
                        color: '#eaeaea',
                        padding: 10,
                        font: { size: 11 }
                    }
                }
            }
        }
    });
}

function createTopAppsChart(chartData) {
    const ctx = document.getElementById('topAppsChart').getContext('2d');

    charts.topApps = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: chartData.labels,
            datasets: [{
                label: 'Install Count',
                data: chartData.data,
                backgroundColor: chartColors.secondary,
                borderWidth: 0,
                borderRadius: 4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                }
            },
            scales: {
                x: {
                    ticks: {
                        color: '#a0a0a0',
                        maxRotation: 45,
                        minRotation: 45
                    },
                    grid: { display: false }
                },
                y: {
                    ticks: { color: '#a0a0a0' },
                    grid: { color: 'rgba(255,255,255,0.1)' }
                }
            }
        }
    });
}

// Load data on page load
document.addEventListener('DOMContentLoaded', loadData);
