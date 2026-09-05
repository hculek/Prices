import esbuild from 'esbuild';
import { glob } from 'glob';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, '..');
const outDir = path.join(projectRoot, 'wwwroot', 'js');
const isWatch = process.argv.includes('--watch');

async function build() {
    console.log('Node build started');

    const siteBuild = {
        entryPoints: [path.join(__dirname, 'js', 'site.js')],
        outbase: path.join(__dirname, 'js'),
        outdir: outDir,
        bundle: true,
        minify: !isWatch,
        sourcemap: true,
        target: ['es2022'],
    };

    const viewEntryPoints = await glob(
        ['Views/**/*.js', 'Areas/**/*.js'],
        {
            cwd: projectRoot,
            absolute: true,
            ignore: [
                '**/node_modules/**',
                'frontend/**',
            ],
        }
    );

    const viewsBuild = {
        entryPoints: viewEntryPoints,
        outbase: projectRoot,
        outdir: outDir,
        bundle: true,
        minify: !isWatch,
        sourcemap: true,
        target: ['es2022'],
    };

    if (isWatch) {
        const ctx1 = await esbuild.context(siteBuild);
        const ctx2 = viewsBuild.entryPoints.length ? await esbuild.context(viewsBuild) : null;
        await ctx1.watch();
        if (ctx2) await ctx2.watch();
    } else {
        await esbuild.build(siteBuild);
        if (viewsBuild.entryPoints.length) {
            await esbuild.build(viewsBuild);
        }
    }

    console.log('Node build finished');
}

build().catch((err) => {
    console.error(err);
    process.exit(1);
});