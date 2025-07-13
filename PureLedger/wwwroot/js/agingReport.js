document.addEventListener('DOMContentLoaded', function () {
    initializeForm();
});

async function initializeForm() {
    try {
        await loadProjects();
        await loadCategories();

        const form = document.getElementById('agingReportForm');
        if (!form) {
            console.error('Form with id "agingReportForm" not found.');
            return;
        }

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            await generateReport();
        });
    } catch (err) {
        console.error('Error initializing form:', err);
    }
}

async function loadProjects() {
    try {
        const select = document.getElementById('projectId');
        if (!select) return;
        // Optionally clear existing options
        // select.innerHTML = '';
        // Optionally skip fetch if options are rendered server-side
        if (select.options.length > 1) return;
        const response = await fetch('/api/metadata/projects');
        const projects = await response.json();
        projects.forEach(project => {
            const option = document.createElement('option');
            option.value = project.id;
            option.textContent = project.name;
            select.appendChild(option);
        });
    } catch (error) {
        console.error('Error loading projects:', error);
    }
}

async function loadCategories() {
    try {
        const select = document.getElementById('categoryId');
        if (!select) return;
        // Optionally clear existing options
        // select.innerHTML = '';
        // Optionally skip fetch if options are rendered server-side
        if (select.options.length > 1) return;
        const response = await fetch('/api/metadata/categories');
        const categories = await response.json();
        categories.forEach(category => {
            const option = document.createElement('option');
            option.value = category.id;
            option.textContent = category.name;
            select.appendChild(option);
        });
    } catch (error) {
        console.error('Error loading categories:', error);
    }
}

async function generateReport() {
    const referenceDate = document.getElementById('referenceDate')?.value;
    const projectId = document.getElementById('projectId')?.value;
    const categoryId = document.getElementById('categoryId')?.value;

    if (!referenceDate || !categoryId) {
        alert('Please select a reference date and category.');
        return;
    }

    try {
        const queryParams = new URLSearchParams({
            referenceDate: referenceDate,
            projectId: projectId,
            categoryId: categoryId
        });

        const response = await fetch(`/Reporting/GetAgingReportData?${queryParams}`);
        const data = await response.json();

        displayReport(data);
    } catch (error) {
        console.error('Error generating report:', error);
        alert('Failed to generate report. Please try again.');
    }
}

function displayReport(data) {
    const tbody = document.getElementById('reportData');
    if (!tbody) return;
    tbody.innerHTML = '';

    data.forEach(item => {
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${item.accountName}</td>
            <td class="text-right">${formatCurrency(item.totalDebit)}</td>
            <td class="text-right">${formatCurrency(item.totalCredit)}</td>
            <td class="text-right">${formatCurrency(item.netBalance)}</td>
        `;
        tbody.appendChild(row);
    });

    document.getElementById('reportResults').style.display = 'block';
}

function formatCurrency(value) {
    return new Intl.NumberFormat('en-US', {
        style: 'decimal',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(value);
}