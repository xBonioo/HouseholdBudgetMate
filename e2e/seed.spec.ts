import { test, expect } from '@playwright/test';

test('user can create expense and see it on the month plan', async (
  { page },
  testInfo
) => {
  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth() + 1;

  const projectCode = testInfo.project.name.slice(0, 2).toUpperCase();
  const uniqueId = Date.now().toString().slice(-6);
  const expenseName = `E2E-${projectCode}-${uniqueId}`;

  await page.goto(`/plan/${year}/${month}?addExpense=true`);

  await expect(page).not.toHaveURL(/login/i);

  await page.getByLabel(/^Nazwa$/).fill(expenseName);
  await page.getByLabel(/^Kategoria$/).click();
  await page.getByRole('option', { name: 'Zdrowie' }).click();
  await page.getByLabel(/^Planowana$/).fill('123,45');
  await page.getByLabel(/^Realna$/).fill('123,45');

  await page
    .getByRole('button', { name: /dodaj wydatek/i })
    .click();

  const expenseRow = page
    .getByRole('row')
    .filter({ hasText: expenseName });

  await expect(expenseRow).toBeVisible({
    timeout: 10_000,
  });

  await expect(expenseRow).toContainText('123,45');
});