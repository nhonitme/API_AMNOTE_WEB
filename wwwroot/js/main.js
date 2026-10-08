// File main.js - custom JS cho toàn dự án
// Bạn có thể thêm các hàm JS thuần hoặc import thư viện ngoài tại đây

function customAlert(msg) {
  alert("Custom JS: " + msg);
}

// Ví dụ sử dụng: customAlert('Xin chào!');
$(document).ready(function () {
  // === BAR CHART ===
  const chartBarEl = document.getElementById('myChart');
  if (chartBarEl) {
    const ctxBar = chartBarEl.getContext('2d');
    new Chart(ctxBar, {
      type: 'bar',
      data: {
        labels: ['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'T8', 'T9', 'T10', 'T11', 'T12'],
        datasets: [{
          label: 'Doanh Thu & Chi Phí',
          data: [50, 60, 55, 70, 65, 68, 75, 80, 72, 85, 82, 84],
          backgroundColor: '#ef4444',
          borderRadius: 6,
          barPercentage: 0.6
        }]
      },
      options: {
        plugins: { legend: { display: false } },
        scales: {
          y: {
            beginAtZero: true,
            ticks: { stepSize: 20 }
          }
        }
      }
    });
  }

  // === LINE CHART ===
  const chartLineEl = document.getElementById('cashFlowChart');
  if (chartLineEl) {
    const ctxLine = chartLineEl.getContext('2d');
    const gradient = ctxLine.createLinearGradient(0, 0, 0, 300);
    gradient.addColorStop(0, 'rgba(239, 68, 68, 0.4)');
    gradient.addColorStop(1, 'rgba(239, 68, 68, 0)');

    new Chart(ctxLine, {
      type: 'line',
      data: {
        labels: ['T1', 'T2', 'T3', 'T4', 'T5', 'T6'],
        datasets: [{
          data: [40, 35, 38, 36, 41, 40],
          fill: true,
          backgroundColor: gradient,
          borderColor: '#ef4444',
          tension: 0.4,
          pointBackgroundColor: '#ef4444',
          pointBorderWidth: 2,
          pointRadius: 5
        }]
      },
      options: {
        plugins: { legend: { display: false } },
        scales: {
          x: { grid: { display: false } },
          y: { beginAtZero: true, grid: { display: false } }
        }
      }
    });
  }

  // === TÌNH HÌNH TÀI CHÍNH ===
  const chartFinEl = document.getElementById('financialChart');
  if (chartFinEl) {
    const ctx = chartFinEl.getContext('2d');
    const values = [38, 857, -1103, 93];
    const backgroundColor = values.map((val, index) => {
      if (val < 0) return '#DB4437';
      return ['#4285F4', '#0F9D58', '#0F9D58', '#F4B400'][index];
    });

    new Chart(ctx, {
      type: 'bar',
      data: {
        labels: ['Tiền mặt', 'Tiền gửi', 'Phải thu', 'Phải trả'],
        datasets: [{
          label: 'Tình hình tài chính',
          data: values,
          backgroundColor: backgroundColor,
          borderColor: backgroundColor,
          borderWidth: 1
        }]
      },
      options: {
        indexAxis: 'y',
        scales: {
          x: {
            beginAtZero: true,
            ticks: {
              callback: value => value.toLocaleString('vi-VN')
            }
          }
        },
        plugins: {
          tooltip: {
            callbacks: {
              label: context => `${context.label}: ${context.raw.toLocaleString('vi-VN')} đ`
            }
          },
          legend: { display: false }
        }
      }
    });
  }

  // === TÌNH HÌNH KINH DOANH ===
  const ctxBusiness = document.getElementById('businessChart')?.getContext('2d');
  if (ctxBusiness) {
    const labels = ['Doanh thu', 'Chi phí', 'Lợi nhuận', 'Hàng tồn kho'];
    const values = [2500, 1800, 700, 1200];
    const backgroundColor = ['#4285F4', '#DB4437', '#0F9D58', '#F4B400'];

    new Chart(ctxBusiness, {
      type: 'bar',
      data: {
        labels: labels,
        datasets: [{
          label: 'Tình hình kinh doanh',
          data: values,
          backgroundColor: backgroundColor,
          borderColor: backgroundColor,
          borderWidth: 1,
          borderRadius: 6,
          barPercentage: 0.6
        }]
      },
      options: {
        responsive: true,
        scales: {
          y: {
            beginAtZero: true,
            ticks: {
              callback: value => value.toLocaleString('vi-VN')
            }
          },
          x: { grid: { display: false } }
        },
        plugins: {
          tooltip: {
            callbacks: {
              label: context => `${context.label}: ${context.raw.toLocaleString('vi-VN')} đ`
            }
          },
          legend: { display: false }
        }
      }
    });
  }

  // === SUBMENU TOGGLE ===
  $('#sidebarMenu .space-y-1 > div').each(function () {
    const $menuBlock = $(this);
    const $submenu = $menuBlock.find('div.submenu').first();
    if ($submenu.length > 0) {
      // always show level‑2 submenu by default (keep first two levels expanded)
      $submenu.show();
      const $mainButton = $menuBlock.find('> button');
      const $toggleIcon = $(
        `<span class="ml-auto text-gray-400 cursor-pointer transition-transform duration-200 chevron rotate-90">
          <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-chevron-right text-gray-400"><path d="m9 18 6-6-6-6"></path></svg>
        </span>
      `).appendTo($mainButton);
      $toggleIcon.on('click', function (e) {
        e.stopPropagation();
        $submenu.slideToggle(200);
        $(this).toggleClass('rotate-90');
      });
    }
  });
});


// === CIRCLE CHARTS ===
$(document).ready(function () {
  const isMobile = window.innerWidth < 768;

  // === PIE CHART: PROFIT ===
  const chartProfitEl = document.getElementById('profitChart');
  if (chartProfitEl) {
    const ctxProfit = chartProfitEl.getContext('2d');
    new Chart(ctxProfit, {
      type: 'pie',
      data: {
        labels: ['Doanh Thu Thuần', 'Chi Phí Hoạt Động', 'Lợi Nhuận'],
        datasets: [{
          data: [68, 25, 7],
          backgroundColor: ['#3b82f6', '#ef4444', '#10b981'],
          borderWidth: 0
        }]
      },
      options: {
        plugins: {
          legend: {
            position: isMobile ? 'bottom' : 'right',
            labels: {
              usePointStyle: true,
              font: { size: 14 },
              generateLabels(chart) {
                const data = chart.data;
                const total = data.datasets[0].data.reduce((a, b) => a + b, 0);
                return data.labels.map((label, i) => {
                  const value = data.datasets[0].data[i];
                  const percentage = ((value / total) * 100).toFixed(1);
                  return {
                    text: `${label} ${percentage}%`,
                    fillStyle: data.datasets[0].backgroundColor[i],
                    strokeStyle: 'transparent',
                    index: i
                  };
                });
              }
            }
          }
        }
      }
    });
  }

  // === PIE CHART: COST OBJECT ===
  const chartCostEl = document.getElementById('costObjectChart');
  if (chartCostEl) {
    const ctxCost = chartCostEl.getContext('2d');
    new Chart(ctxCost, {
      type: 'pie',
      data: {
        labels: ['Sản xuất chính', 'Bán hàng & Marketing', 'Quản lý chung', 'Nghiên cứu phát triển'],
        datasets: [{
          data: [40.9, 31.8, 18.2, 9.1],
          backgroundColor: ['#3b82f6', '#ef4444', '#10b981', '#f59e0b'],
          borderWidth: 0
        }]
      },
      options: {
        plugins: {
          legend: {
            position: isMobile ? 'bottom' : 'right',
            labels: {
              usePointStyle: true,
              font: { size: 14 },
              generateLabels(chart) {
                const data = chart.data;
                const total = data.datasets[0].data.reduce((a, b) => a + b, 0);
                return data.labels.map((label, i) => {
                  const value = data.datasets[0].data[i];
                  const percentage = ((value / total) * 100).toFixed(1);
                  return {
                    text: `${label} ${percentage}%`,
                    fillStyle: data.datasets[0].backgroundColor[i],
                    strokeStyle: 'transparent',
                    index: i
                  };
                });
              }
            }
          }
        }
      }
    });
  }
});


// === MENU FILTER FUNCTION ===
function filterMenu(keyword) {
  const lowerKeyword = keyword.toLowerCase();
  $("#sidebarMenu .menu-item").each(function () {
    const $menuItem = $(this);
    const textContent = $menuItem.text().toLowerCase();
    const match = textContent.includes(lowerKeyword);
    if (match) {
      $menuItem.show();
      $menuItem.find("h3").show();
    } else {
      $menuItem.hide();
    }
  });
  if (!keyword) {
    $("#sidebarMenu .menu-item").show();
    $("#sidebarMenu h3").show();
  }
}
