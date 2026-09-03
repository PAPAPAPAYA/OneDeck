module.exports = {
	apps: [{
		name: 'onedeck-api',
		script: 'server.js',
		cwd: __dirname,
		env: {
			NODE_ENV: 'production',
			PORT: '3000',
			HOST: '127.0.0.1'
		},
		autorestart: true,
		watch: false,
		max_memory_restart: '200M',
		out_file: '../data/pm2-out.log',
		error_file: '../data/pm2-error.log'
	}]
};
