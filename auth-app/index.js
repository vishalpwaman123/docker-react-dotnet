const express = require('express');
const { createClient } = require('redis');
const { Pool } = require('pg');

const app = express();
const PORT = process.env.PORT || 3000;

async function init() {
  // --- Redis Connection ---
  const redisClient = createClient({
    socket: {
      host: process.env.REDIS_HOST || 'localhost',
      port: process.env.REDIS_PORT || 6379,
    },
  });

  redisClient.on('error', (err) => console.error('Redis error:', err));

  try {
    await redisClient.connect();
    console.log('Redis connected successfully');
  } catch (err) {
    console.error('Redis connection failed:', err.message);
  }

  // --- Postgres Connection ---
  const pgPool = new Pool({
    host:     process.env.PG_HOST     || 'localhost',
    port:     process.env.PG_PORT     || 5432,
    database: process.env.PG_DB       || 'postgres',
    user:     process.env.PG_USER     || 'postgres',
    password: process.env.PG_PASSWORD || 'postgres',
  });

  try {
    const client = await pgPool.connect();
    console.log('Postgres connected successfully');
    client.release(); // return the connection back to the pool
  } catch (err) {
    console.error('Postgres connection failed:', err.message);
  }
}

// Health-check route
app.get('/', (req, res) => {
  res.send('Server is running!');
});

// Start server, then run init()
app.listen(PORT, async () => {
  console.log(`Server started on port ${PORT}`);
  await init();
});
