import { expect, test } from '@playwright/test';

const money = {
  planned: '400,00',
  actualBeforeEdit: '100,00',
  actualAfterEdit: '534,56',
};

const editedActualAmountInCents = 53_456;

test('monthly expense edit stays visible across Plan, Dashboard, Accounts, and Statistics', async ({
  page,
}, testInfo) => {
  test.setTimeout(60_000);

  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth() + 1;
  const projectCode = testInfo.project.name.slice(0, 2).toUpperCase();
  const uniqueId = Date.now().toString().slice(-6);
  const expenseName = `E2E-XSCREEN-${projectCode}-${uniqueId}`;

  const expensesTotalBefore = await readAccountsMonthlyExpenses(page);

  await page.goto(`/plan/${year}/${month}?addExpense=true`, {
    waitUntil: 'domcontentloaded',
  });
  await expect(page).not.toHaveURL(/login/i);
  await expect(
    page.getByRole('heading', { level: 4, name: /^wydatki\b/i })
  ).toBeVisible();

  await page.getByLabel(/^Nazwa$/).fill(expenseName);
  await page.getByLabel(/^Kategoria$/).click();
  await page.getByRole('option', { name: 'Zdrowie' }).click();
  await page.getByLabel(/^Planowana$/).fill(money.planned);
  await page.getByLabel(/^Realna$/).fill(money.actualBeforeEdit);
  await page.getByRole('button', { name: /dodaj wydatek/i }).click();

  const planRow = page.getByRole('row').filter({ hasText: expenseName });
  await expect(planRow).toBeVisible({ timeout: 10_000 });
  await expect(planRow).toContainText(money.actualBeforeEdit);

  await planRow.getByRole('button', { name: /edytuj/i }).click();
  await expect(page.getByText('Edycja wydatku')).toBeVisible();
  await page.getByLabel(/^Realna$/).first().fill(money.actualAfterEdit);
  await page.getByRole('button', { name: /^Zapisz$/ }).click();

  await expect(planRow).toContainText(money.actualAfterEdit, { timeout: 10_000 });

  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: /dashboard bud/i })).toBeVisible();
  const dashboardRecentExpense = page.locator('.mud-list-item').filter({ hasText: expenseName });
  await expect(dashboardRecentExpense).toBeVisible({ timeout: 10_000 });
  await expect(dashboardRecentExpense).toContainText(money.actualAfterEdit);

  await page.goto('/accounts', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: /konta i/i })).toBeVisible();
  await expect(page.getByText('Wpływy / wydatki')).toBeVisible();
  await expect(page.getByText('Kategorie przekroczone')).toBeVisible();
  const expensesTotalAfter = await readAccountsMonthlyExpenses(page);
  expect(toCents(expensesTotalAfter)).toBeGreaterThanOrEqual(
    toCents(expensesTotalBefore) + editedActualAmountInCents
  );

  await page.goto('/statistics', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: /statystyki roczne/i })).toBeVisible();
  await page.getByLabel('Nazwa lub opis').fill(expenseName);
  await page.getByRole('button', { name: /^Szukaj$/ }).first().click();

  const statisticsRow = page.getByRole('row').filter({ hasText: expenseName });
  await expect(statisticsRow).toBeVisible({ timeout: 10_000 });
  await expect(statisticsRow).toContainText('Zdrowie');
  await expect(statisticsRow).toContainText(money.actualAfterEdit);
});

async function readAccountsMonthlyExpenses(page: import('@playwright/test').Page) {
  await page.goto('/accounts', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: /konta i/i })).toBeVisible();

  const movementCardText = await page
    .locator('.accounts-kpi-card')
    .filter({ hasText: 'Wpływy / wydatki' })
    .innerText();

  const amounts = [...movementCardText.matchAll(/-?[\d\s]+,\d{2}/g)].map(
    (match) => match[0]
  );
  const expensesAmount = amounts.at(-1);

  if (!expensesAmount) {
    throw new Error(`Could not read monthly expenses from: ${movementCardText}`);
  }

  return Number(expensesAmount.replace(/\s/g, '').replace(',', '.'));
}

function toCents(amount: number) {
  return Math.round(amount * 100);
}
