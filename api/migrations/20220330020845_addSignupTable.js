exports.up = async (knex) => {
    await knex.schema.createTable('join_application', (t) => {
        t.string('id', 128).notNullable(); // app guid
        t.string('preferred_name', 512).notNullable(); // name the user wants to go by, either real name or a username
        t.string('about', 4096).notNullable(); // about me section
        t.string('matrix_name', 512).notNullable(); // first part of matrix name
        t.string('matrix_domain', 512).notNullable(); // matrix homeserver
        t.string('social_presence', 512).notNullable(); // a valid social presence, such as a twitter account with followers, youtube channel, etc
        // discord info (why does pekora not change their migrations tf)
        t.string('discord_id', 32).nullable().defaultTo(null);
        t.string('discord_username', 64).nullable().defaultTo(null);
        t.bigInteger('user_id').nullable().defaultTo(null); // null if app hasn't been used yet
        t.bigInteger('author_id').nullable().defaultTo(null); // staff member who accepted/declined the app
        t.string('reject_reason', 512).nullable().defaultTo(null); // reason for rejection, if app is rejected
        t.integer('status').notNullable().defaultTo(1); // app status enum
        
        t.dateTime("created_at").notNullable().defaultTo(knex.fn.now());
        t.dateTime("updated_at").notNullable().defaultTo(knex.fn.now());
    });
};

exports.down = async (knex) => {
    await knex.schema.dropTable('join_application');
};
