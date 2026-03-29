/**
 * Add place data to asset
 * @param {import('knex')} knex 
 */
exports.up = async (knex) => {
    await knex.schema.createTable('asset_place', (t) => {
        t.bigInteger('asset_id').unsigned().notNullable();
        t.integer('max_player_count').notNullable().unsigned().defaultTo(10);
        // general
        t.integer('server_fill_mode').notNullable().unsigned().defaultTo(1); // 1 = Roblox Optimize, 2 = Fill, 3 = Custom
        t.integer('server_slot_size').nullable().defaultTo(null); // must not be null if above = 3
        // vip
        t.boolean('is_vip_enabled').notNullable().defaultTo(false);
        t.integer('vip_price').nullable().defaultTo(null); // null = free
        // perms
        t.boolean('is_public_domain').notNullable().defaultTo(false);
        t.integer('access').notNullable().defaultTo(1); // 1 = Everyone, 2 = Friends/Group Members (depending on creator_type)
        t.bigInteger('visit_count').notNullable().defaultTo(0);
		t.integer('year').notNullable().unsigned().defaultTo(2017);
    });
    // migrate existing
    const places = await knex('asset').select('*').where({ 'asset_type': 9 });
    for (const p of places) {
        console.log('[info] add asset place data for', p.id);
        await knex('asset_place').insert({
            'asset_id': p.id,
            'max_player_count': 10,
            'server_fill_mode': 1,
            'access': 1,
            'is_vip_enabled': false,
            'is_public_domain': false,
			'year': 2017,
        })
    }
};

exports.down = async (knex) => {
    await knex.schema.dropTable('asset_place');
};
