const { PrismaClient, Role } = require('@prisma/client');
const prisma = new PrismaClient();

async function main() {
    // Plans
    await prisma.plan.createMany({
        data: [
            { name: 'Basic', durationMonths: 3, priceCents: 9000 },
            { name: 'Premium', durationMonths: 6, priceCents: 10000 },
            { name: 'Silver', durationMonths: 12, priceCents: 16000 },
            { name: 'Golden', durationMonths: 24, priceCents: 19000 },
            { name: 'Platinum', durationMonths: 36, priceCents: 25000 }
        ],
        skipDuplicates: true
    });
}

main()
    .then(() => prisma.$disconnect())
    .catch(async (e) => { console.error(e); await prisma.$disconnect(); process.exit(1); });
