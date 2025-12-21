export const getDateRange = (filterType) => {
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    
    let startDate = null;
    let endDate = null;

    switch (filterType) {
        case 'today':
            startDate = new Date(today);
            endDate = new Date(today);
            break;
        
        case 'thisWeek':
            const dayOfWeek = today.getDay();
            const diff = today.getDate() - dayOfWeek + (dayOfWeek === 0 ? -6 : 1);
            startDate = new Date(today.getFullYear(), today.getMonth(), diff);
            endDate = new Date(today);
            break;
        
        case 'thisMonth':
            startDate = new Date(today.getFullYear(), today.getMonth(), 1);
            endDate = new Date(today);
            break;
        
        case 'thisQuarter':
            const quarter = Math.floor(today.getMonth() / 3);
            startDate = new Date(today.getFullYear(), quarter * 3, 1);
            endDate = new Date(today);
            break;
        
        case 'thisYear':
            startDate = new Date(today.getFullYear(), 0, 1);
            endDate = new Date(today);
            break;
        
        case 'lastWeek':
            const lastWeekDay = today.getDay();
            const lastWeekDiff = today.getDate() - lastWeekDay - 6 + (lastWeekDay === 0 ? -6 : 1);
            startDate = new Date(today.getFullYear(), today.getMonth(), lastWeekDiff);
            const lastWeekEnd = new Date(startDate);
            lastWeekEnd.setDate(startDate.getDate() + 6);
            endDate = lastWeekEnd;
            break;
        
        case 'lastMonth':
            startDate = new Date(today.getFullYear(), today.getMonth() - 1, 1);
            endDate = new Date(today.getFullYear(), today.getMonth(), 0);
            break;
        
        case 'lastQuarter':
            const currentQuarter = Math.floor(today.getMonth() / 3);
            const lastQuarterStart = currentQuarter === 0 ? 9 : (currentQuarter - 1) * 3;
            const lastQuarterYear = currentQuarter === 0 ? today.getFullYear() - 1 : today.getFullYear();
            startDate = new Date(lastQuarterYear, lastQuarterStart, 1);
            endDate = new Date(today.getFullYear(), currentQuarter * 3, 0);
            break;
        
        case 'lastYear':
            startDate = new Date(today.getFullYear() - 1, 0, 1);
            endDate = new Date(today.getFullYear() - 1, 11, 31);
            break;
        
        case 'last7Days':
            startDate = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 6);
            endDate = new Date(today);
            break;
        
        case 'last30Days':
            startDate = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 29);
            endDate = new Date(today);
            break;
        
        case 'last90Days':
            startDate = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 89);
            endDate = new Date(today);
            break;
        
        case 'custom':
        default:
            return { startDate: '', endDate: '' };
    }

    const formatDateForInput = (date) => {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    };

    return {
        startDate: formatDateForInput(startDate),
        endDate: formatDateForInput(endDate)
    };
};

export const dateFilterOptions = [
    { value: 'custom', label: 'Custom Range', icon: 'fa-calendar-alt' },
    { value: 'today', label: 'Today', icon: 'fa-calendar-day' },
    { value: 'thisWeek', label: 'This Week', icon: 'fa-calendar-week' },
    { value: 'thisMonth', label: 'This Month', icon: 'fa-calendar' },
    { value: 'thisQuarter', label: 'This Quarter', icon: 'fa-calendar-check' },
    { value: 'thisYear', label: 'This Year', icon: 'fa-calendar-alt' },
    { value: 'lastWeek', label: 'Last Week', icon: 'fa-calendar-week' },
    { value: 'lastMonth', label: 'Last Month', icon: 'fa-calendar' },
    { value: 'lastQuarter', label: 'Last Quarter', icon: 'fa-calendar-check' },
    { value: 'lastYear', label: 'Last Year', icon: 'fa-calendar-alt' },
    { value: 'last7Days', label: 'Last 7 Days', icon: 'fa-clock' },
    { value: 'last30Days', label: 'Last 30 Days', icon: 'fa-clock' },
    { value: 'last90Days', label: 'Last 90 Days', icon: 'fa-clock' }
];

