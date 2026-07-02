const Database = require('better-sqlite3');
const db = new Database('C:\\Work\\Zaiko\\Zaiko\\app.db', { readonly: true });

const users = db.prepare('SELECT UserName, Email, EmailConfirmed FROM AspNetUsers').all();
console.log('登録ユーザー数:', users.length);
users.forEach(u => console.log(' -', u.UserName, '/', u.Email));
db.close();
