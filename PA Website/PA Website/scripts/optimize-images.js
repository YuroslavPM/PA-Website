/**
 * Image Optimization Script for PA Website
 * Generates responsive image sizes for Author.webp and logo.webp
 * Run with: npm run optimize:images
 */

const sharp = require('sharp');
const path = require('path');
const fs = require('fs');

const IMAGES_DIR = path.join(__dirname, '..', 'wwwroot', 'Images', 'siteImg');

// Image optimization configurations
const imageConfigs = [
    // Author image - keep a large canonical file for Google, plus display sizes
    {
        source: 'Author.webp',
        sizes: [160, 256, 320, 384, 800, 1200],
        suffix: (size) => `-${size}`,
        quality: 88
    },
    // Logo image - used at 80px, 96px, 112px (with 2x for retina)
    {
        source: 'logo.webp',
        sizes: [80, 96, 112],
        suffix: (size) => `-${size}`,
        quality: 90
    }
];

async function optimizeImage(config) {
    const sourcePath = path.join(IMAGES_DIR, config.source);
    
    if (!fs.existsSync(sourcePath)) {
        console.error(`❌ Source file not found: ${sourcePath}`);
        return;
    }

    console.log(`\n📷 Processing ${config.source}...`);
    
    for (const size of config.sizes) {
        const outputName = config.source.replace('.webp', `${config.suffix(size)}.webp`);
        const outputPath = path.join(IMAGES_DIR, outputName);
        
        try {
            await sharp(sourcePath)
                .resize(size, size, {
                    fit: 'cover',
                    position: 'center'
                })
                .webp({ quality: config.quality })
                .toFile(outputPath);
            
            const stats = fs.statSync(outputPath);
            const sizeKB = (stats.size / 1024).toFixed(1);
            console.log(`  ✅ Created ${outputName} (${size}x${size}) - ${sizeKB} KB`);
        } catch (error) {
            console.error(`  ❌ Failed to create ${outputName}: ${error.message}`);
        }
    }
}

async function main() {
    console.log('🖼️  PA Website Image Optimization');
    console.log('================================');
    
    // Check if source directory exists
    if (!fs.existsSync(IMAGES_DIR)) {
        console.error(`❌ Images directory not found: ${IMAGES_DIR}`);
        process.exit(1);
    }
    
    // Process each image configuration
    for (const config of imageConfigs) {
        await optimizeImage(config);
    }
    
    console.log('\n✨ Image optimization complete!');
    console.log('\nGenerated responsive images for:');
    console.log('  - Author.webp: 160px, 256px, 320px, 384px, 800px, 1200px variants');
    console.log('  - logo.webp: 80px, 96px, 112px variants');
}

main().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});











