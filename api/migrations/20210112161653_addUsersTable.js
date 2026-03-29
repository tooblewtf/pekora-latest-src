// initial users table

// notes:
// email relationship is one to many (one user to many emails, although only latest can be used, even if not verified)
// password relationship is one to one (only one password per user - we dont save historical)
// username table must exist for changing names (one to many, but only one in use at any given time)
// birth date is only saved once (one to one, no need for historical data)
// description isn't saved anywhere besides users table (nullable)
// user status will have to be in own table since one to many (old statuses are saved)


// todo: bans table, username history table, user thumbnails table
/**
 * Create users table
 * @param {import('knex')} knex 
 */
exports.up = async (knex) => {
    await knex.schema.createTable('user', (t) => {
        // The userId
        t.bigIncrements('id').notNullable().unsigned();
        // The username
        t.string('username', 64).notNullable();
        // The password
        t.string('password', 255).notNullable();
		// discord id since it didn't exist in the migration
		t.string('discord_id', 32).nullable().defaultTo(null);
		// is user a verified user
		t.boolean('verified').notNullable().defaultTo(false);
        // Internal status
        // 1 = OK, 2 = Suppressed, 3 = Deleted, 4 = Poisoned, 5 = MustValidateEmail, 6 = Forgotten
        // If 6, all user data must be hidden as if the user doesn't exist, and name must be replaced with "[ Account Deleted id ]". 3 and 4 are the same. 5 is locked. 2 is temporary ban.
        t.integer('status').notNullable().unsigned().defaultTo(1).notNullable();
        // Date the user was created (aka join date)
        t.dateTime("created_at").notNullable().defaultTo(knex.fn.now());
        // User description/bio
        t.string('description', 1024).nullable().defaultTo(null);
        // Date the user was last online
        t.dateTime('online_at').notNullable().defaultTo(knex.fn.now());
        // Prevent duplicate usernames
        t.unique(['username']);
    });
};

/**
 * Drop the user table
 * @param {import('knex')} knex 
 */
exports.down = async (knex) => {
    await knex.schema.dropTable('user');
};
