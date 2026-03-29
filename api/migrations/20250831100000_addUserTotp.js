// initial totp (2fa) table made by skylerclock

export async function up(knex) {
  await knex.schema.createTable("user_totp", (table) => {
    table.increments("id").primary();
    table.string("secret").notNullable();
    table.integer("user_id").unsigned().notNullable().index();
    table.string("status").notNullable();
    table.timestamps(true, true);
  });
}

export async function down(knex) {
  await knex.schema.dropTableIfExists("user_totp");
}