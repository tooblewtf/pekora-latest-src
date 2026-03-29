'use client'

import CreateHelpPage from "../../components/help/create";
import Theme2016 from "../../components/theme2016";
import Head from "next/head";

const CreateHelp = props => {
    return (
        <Theme2016>
            <Head>
                <link rel="preload" href="/markdown/createGame.md" as="fetch" crossOrigin="anonymous" />
            </Head>
            <CreateHelpPage />
        </Theme2016>
    );
}

export const getStaticProps = () => {
    return {
        props: {
            title: 'Creating Help - Marine',
            disableWebsiteTheming: true,
        },
    };
};

export default CreateHelp;