/**
 * Water District System — App Shell Script
 *
 * Scope:
 *  - Theme toggle (dark/light) with localStorage persistence
 *  - Sidebar accordion (submenu expand/collapse)
 *  - Active nav-link state management
 *  - Mobile sidebar drawer open/close
 */

/* ============================================================
   THEME
   ============================================================ */

(function applyThemeEarly() {
  /**
   * Run immediately (IIFE) before DOM renders to avoid
   * a flash of the wrong theme.
   */
  const saved = localStorage.getItem('wds-theme');

  if (saved === 'light' || saved === 'dark') {
    document.documentElement.dataset.theme = saved;
  } else {
    // No saved preference — defer to OS preference, default dark.
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    document.documentElement.dataset.theme = prefersDark ? 'dark' : 'light';
  }
}());


/**
 * Update the theme toggle button's aria-label and aria-pressed
 * to reflect the currently active theme.
 *
 * @param {HTMLElement} btn - The theme toggle button element.
 * @param {string} theme - 'dark' or 'light'.
 */
function updateThemeToggleUI(btn, theme) {
  if (!btn) return;
  if (theme === 'light') {
    btn.setAttribute('aria-label', 'Switch to dark mode');
    btn.setAttribute('aria-pressed', 'false');
  } else {
    btn.setAttribute('aria-label', 'Switch to light mode');
    btn.setAttribute('aria-pressed', 'true');
  }
}


/**
 * Toggle the theme between 'dark' and 'light', persist to
 * localStorage, and update the toggle button state.
 */
function handleThemeToggle() {
  const current = document.documentElement.dataset.theme || 'dark';
  const next = current === 'dark' ? 'light' : 'dark';

  document.documentElement.dataset.theme = next;
  localStorage.setItem('wds-theme', next);

  const btn = document.getElementById('themeToggle');
  updateThemeToggleUI(btn, next);
}


/* ============================================================
   ACCORDION (SUBMENUS)
   ============================================================ */

/**
 * Open a submenu by its trigger button element.
 * Closes all other open submenus (one-open-at-a-time accordion).
 *
 * @param {HTMLElement} trigger - The accordion trigger button.
 */
function openAccordion(trigger) {
  const controlsId = trigger.getAttribute('aria-controls');
  const submenu = document.getElementById(controlsId);
  if (!submenu) return;

  // Close all other open accordions first
  document.querySelectorAll('.nav-accordion-trigger[aria-expanded="true"]').forEach(function(otherTrigger) {
    if (otherTrigger !== trigger) {
      closeAccordion(otherTrigger);
    }
  });

  trigger.setAttribute('aria-expanded', 'true');
  submenu.classList.add('is-open');
}


/**
 * Close a submenu by its trigger button element.
 *
 * @param {HTMLElement} trigger - The accordion trigger button.
 */
function closeAccordion(trigger) {
  const controlsId = trigger.getAttribute('aria-controls');
  const submenu = document.getElementById(controlsId);
  if (!submenu) return;

  trigger.setAttribute('aria-expanded', 'false');
  submenu.classList.remove('is-open');
}


/**
 * Toggle a submenu open or closed.
 *
 * @param {HTMLElement} trigger - The accordion trigger button.
 */
function toggleAccordion(trigger) {
  const isOpen = trigger.getAttribute('aria-expanded') === 'true';
  if (isOpen) {
    closeAccordion(trigger);
  } else {
    openAccordion(trigger);
  }
}


/* ============================================================
   ACTIVE LINK STATE
   ============================================================ */

/**
 * Mark a nav link as active, clearing the active state from
 * all other nav links.
 *
 * @param {HTMLElement} link - The nav link element to activate.
 */
function setActiveLink(link) {
  document.querySelectorAll('.nav-link.is-active').forEach(function(el) {
    el.classList.remove('is-active');
  });
  link.classList.add('is-active');
}


/* ============================================================
   MOBILE SIDEBAR DRAWER
   ============================================================ */

/**
 * Open the sidebar drawer (mobile).
 */
function openSidebar() {
  const sidebar = document.getElementById('sidebar');
  const overlay = document.getElementById('sidebarOverlay');
  const hamburger = document.getElementById('hamburgerBtn');

  if (sidebar)  sidebar.classList.add('is-open');
  if (overlay)  overlay.classList.add('is-visible');
  if (hamburger) hamburger.setAttribute('aria-expanded', 'true');
  if (sidebar)  sidebar.setAttribute('aria-hidden', 'false');
}


/**
 * Close the sidebar drawer (mobile).
 */
function closeSidebar() {
  const sidebar = document.getElementById('sidebar');
  const overlay = document.getElementById('sidebarOverlay');
  const hamburger = document.getElementById('hamburgerBtn');

  if (sidebar)  sidebar.classList.remove('is-open');
  if (overlay)  overlay.classList.remove('is-visible');
  if (hamburger) hamburger.setAttribute('aria-expanded', 'false');
  if (sidebar)  sidebar.setAttribute('aria-hidden', 'true');
}


/**
 * Toggle the sidebar drawer open or closed (mobile).
 */
function toggleSidebar() {
  const sidebar = document.getElementById('sidebar');
  if (!sidebar) return;

  if (sidebar.classList.contains('is-open')) {
    closeSidebar();
  } else {
    openSidebar();
  }
}


/* ============================================================
   PAGE LOADER
   ============================================================ */

/**
 * Registry of already-loaded page assets to avoid duplicate
 * <link> and <script> injections on repeat navigation.
 * @type {Set<string>}
 */
var loadedPageAssets = new Set();


/**
 * Dynamically load a <link rel="stylesheet"> for a module if not
 * already present in the document.
 *
 * @param {string} pageName - The page identifier (e.g. 'reading-sheet').
 * @returns {Promise<void>}
 */
function loadPageCSS(pageName) {
  var assetKey = 'css:' + pageName;
  if (loadedPageAssets.has(assetKey)) {
    return Promise.resolve();
  }
  return new Promise(function(resolve) {
    var link = document.createElement('link');
    link.rel  = 'stylesheet';
    link.href = 'css/' + pageName + '.css';
    link.onload = function() {
      loadedPageAssets.add(assetKey);
      resolve();
    };
    link.onerror = function() {
      console.warn('[Shell] Could not load CSS for page: ' + pageName);
      resolve();
    };
    document.head.appendChild(link);
  });
}


/**
 * Dynamically load any script by src path if not already loaded.
 * Used for data files and page scripts alike.
 *
 * @param {string} src - Relative path to the script (e.g. 'modules/reading-sheet/reading-sheet-sample.js').
 * @returns {Promise<void>}
 */
function loadScript(src) {
  var assetKey = 'script:' + src;
  if (loadedPageAssets.has(assetKey)) {
    return Promise.resolve();
  }
  return new Promise(function(resolve) {
    var script = document.createElement('script');
    script.src = src;
    script.onload = function() {
      loadedPageAssets.add(assetKey);
      resolve();
    };
    script.onerror = function() {
      console.warn('[Shell] Could not load script: ' + src);
      resolve();
    };
    document.body.appendChild(script);
  });
}


/**
 * Dynamically load the main script for a module if not already present.
 * Thin wrapper around loadScript() for named module scripts.
 *
 * @param {string} pageName - The page identifier (e.g. 'reading-sheet').
 * @returns {Promise<void>}
 */
function loadPageScript(pageName) {
  return loadScript('js/' + pageName + '.js');
}


/**
 * Page data file registry.
 * Maps a pageName to one or more data scripts that must be
 * loaded (in order) before the page script runs.
 * Add entries here as new pages with data files are introduced.
 */
/**
 * Page data file registry.
 * Maps a pageName to one or more data scripts that must be
 * loaded (in order) before the page script runs.
 * Add entries here as new pages with data files are introduced.
 */
var PAGE_DATA_FILES = {
  'reading-sheet': ['js/reading-sheet-sample.js']
};


/**
 * Load a page fragment into #pageContent.
 *
 * Sequence:
 *  1. Fetch pages/{pageName}.html
 *  2. Inject HTML into #pageContent
 *  3. Ensure styles/{pageName}.css is loaded
 *  4. Load any data scripts for this page (in order, before page script)
 *  5. Ensure scripts/{pageName}.js is loaded
 *  6. Call the page's init function (window['init' + PascalCase + 'Page'])
 *
 * @param {string} pageName  - The page identifier (e.g. 'reading-sheet').
 * @param {string} [pageTitle] - Optional title to display in #pageTitle.
 */
function loadPage(pageName, pageTitle) {
  var pageContent = document.getElementById('pageContent');
  var pageTitleEl = document.getElementById('pageTitle');

  if (!pageContent) return;

  fetch('modules/' + pageName + '/' + pageName + '.html')
    .then(function(response) {
      if (!response.ok) {
        throw new Error('Failed to fetch page: ' + pageName + ' (' + response.status + ')');
      }
      return response.text();
    })
    .then(function(html) {
      // Inject fragment
      pageContent.innerHTML = html;

      // Update page title if provided
      if (pageTitleEl && pageTitle) {
        pageTitleEl.textContent = pageTitle;
      }

      // Load CSS immediately (no ordering dependency)
      var cssPromise = loadPageCSS(pageName);

      // Load data files in sequence, then the page script
      var dataFiles = PAGE_DATA_FILES[pageName] || [];
      var scriptChain = dataFiles.reduce(function(chain, src) {
        return chain.then(function() {
          return loadScript(src);
        });
      }, Promise.resolve());

      var jsPromise = scriptChain.then(function() {
        return loadPageScript(pageName);
      });

      return Promise.all([cssPromise, jsPromise]);
    })
    .then(function() {
      // Build init function name: 'reading-sheet' → 'initReadingSheetPage'
      var initFnName = 'init' + pageName
        .split('-')
        .map(function(part) {
          return part.charAt(0).toUpperCase() + part.slice(1);
        })
        .join('') + 'Page';

      if (typeof window[initFnName] === 'function') {
        window[initFnName]();
      } else {
        console.warn('[Shell] No init function found: ' + initFnName);
      }
    })
    .catch(function(err) {
      console.error('[Shell] loadPage error:', err);
      pageContent.innerHTML = '<p style="color:var(--color-red);padding:var(--space-md)">Failed to load page.</p>';
    });
}




document.addEventListener('DOMContentLoaded', function() {

  // --- Apply correct theme toggle UI after DOM is ready ---
  const themeToggleBtn = document.getElementById('themeToggle');
  const currentTheme = document.documentElement.dataset.theme || 'dark';
  updateThemeToggleUI(themeToggleBtn, currentTheme);


  // --- Theme toggle ---
  if (themeToggleBtn) {
    themeToggleBtn.addEventListener('click', handleThemeToggle);
  }


  // --- Accordion triggers ---
  document.querySelectorAll('.nav-accordion-trigger').forEach(function(trigger) {
    trigger.addEventListener('click', function() {
      toggleAccordion(trigger);
    });
  });


  // --- Active link: nav <a> links only ---
  document.querySelectorAll('.sidebar .nav-link:not(.nav-accordion-trigger)').forEach(function(link) {
    link.addEventListener('click', function() {
      setActiveLink(link);

      // Close sidebar drawer on mobile after a link is tapped
      if (window.innerWidth <= 600) {
        closeSidebar();
      }
    });
  });


  // --- Page navigation: wire data-nav links to loadPage() ---
  var readingSheetLink = document.querySelector('[data-nav="reading-sheet"]');
  if (readingSheetLink) {
    readingSheetLink.addEventListener('click', function(e) {
      e.preventDefault();
      loadPage('reading-sheet', 'View Reading Sheet Created');
    });
  }


  // --- Mobile hamburger ---
  const hamburgerBtn = document.getElementById('hamburgerBtn');
  if (hamburgerBtn) {
    hamburgerBtn.addEventListener('click', toggleSidebar);
  }


  // --- Overlay click closes drawer ---
  const overlay = document.getElementById('sidebarOverlay');
  if (overlay) {
    overlay.addEventListener('click', closeSidebar);
  }


  // --- Close drawer on resize if viewport widens past breakpoint ---
  window.addEventListener('resize', function() {
    if (window.innerWidth > 600) {
      closeSidebar();
    }
  });


  // --- Auto-open accordion group and mark active link based on current URL ---
  // Matches the current path against each nav link's href so the correct
  // submenu stays open and the correct item is highlighted on full-page loads.
  (function autoActivateNav() {
    var currentPath = window.location.pathname.toLowerCase();

    // Find all non-trigger nav links and check if their href matches the URL
    var allLinks = document.querySelectorAll('.sidebar .nav-link:not(.nav-accordion-trigger)');
    var matched = null;

    allLinks.forEach(function(link) {
      var href = (link.getAttribute('href') || '').toLowerCase();
      // Exact match or current path starts with href (covers sub-routes)
      if (href && href !== '#' && currentPath === href) {
        matched = link;
      }
    });

    // Looser match if exact didn't find anything (last segment comparison)
    if (!matched) {
      allLinks.forEach(function(link) {
        var href = (link.getAttribute('href') || '').toLowerCase();
        if (href && href !== '#' && currentPath.startsWith(href)) {
          matched = link;
        }
      });
    }

    if (!matched) return;

    // Mark it active
    setActiveLink(matched);

    // Walk up the DOM to find a parent nav-submenu and open its accordion
    var submenu = matched.closest('.nav-submenu');
    if (submenu) {
      var trigger = document.querySelector('[aria-controls="' + submenu.id + '"]');
      if (trigger) openAccordion(trigger);
    }
  }());

});
